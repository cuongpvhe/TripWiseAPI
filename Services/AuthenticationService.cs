using Google.Apis.Auth;
using TripWiseAPI.Messages;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models;
using TripWiseAPI.Utils;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Services.AdminServices;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace TripWiseAPI.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly TripWiseDBContext _context;
        private readonly IConfiguration _config;
        private readonly IAppSettingsService _appSettingsService;

        public AuthenticationService(TripWiseDBContext context, IConfiguration config, IAppSettingsService appSettingsService)
        {
            _context = context;
            _config = config;
            _appSettingsService = appSettingsService;
        }

        public async Task<(string accessToken, string refreshToken)> LoginAsync(LoginModel loginModel)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginModel.Email && u.IsActive);
            if (user == null || !PasswordHelper.VerifyPasswordBCrypt(loginModel.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");

            await DeleteOldRefreshToken(user.UserId, loginModel.DeviceId);

            var accessToken = JwtHelper.GenerateJwtToken(_config, user);
            var refreshToken = JwtHelper.GenerateRefreshToken();

            await _context.UserRefreshTokens.AddAsync(new UserRefreshToken
            {
                UserId = user.UserId,
                RefreshToken = refreshToken,
                DeviceId = loginModel.DeviceId,
                CreatedAt = TimeHelper.GetVietnamTime(),
                ExpiresAt = TimeHelper.GetVietnamTime().AddMonths(1)
            });

            await _context.SaveChangesAsync();
            return (accessToken, refreshToken);
        }

        public async Task<(string accessToken, string refreshToken)> GoogleLoginAsync(GoogleLoginModel model)
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(model.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] },
                IssuedAtClockTolerance = TimeSpan.FromMinutes(5)
            });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);
            if (user == null)
            {
                user = new User
                {
                    Email = payload.Email,
                    UserName = payload.Email.Split('@')[0],
                    CreatedDate = TimeHelper.GetVietnamTime(),
                    IsActive = true,
                    Role = "USER",
                    PasswordHash = ""
                };
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                _ = Task.Run(() => EmailHelper.SendEmailMultiThread(
                    user.Email,
                    "Chào mừng đến với TripWise!",
                    $"Xin chào {user.UserName}, cảm ơn bạn đã đăng ký TripWise!"));

                // Gán gói Trial hoặc Free
                string? trialPlanName = await _appSettingsService.GetValueAsync("DefaultTrialPlanName");
                string? freePlanName = await _appSettingsService.GetValueAsync("FreePlan");
                int trialDuration = await _appSettingsService.GetIntValueAsync("TrialDurationInDays", 90);

                Plan? planToAssign = null;
                DateTime? endDate = null;

                if (!string.IsNullOrEmpty(trialPlanName))
                {
                    planToAssign = await _context.Plans
                        .FirstOrDefaultAsync(p => p.PlanName == trialPlanName && p.RemovedDate == null);

                    if (planToAssign != null)
                    {
                        endDate = TimeHelper.GetVietnamTime().AddDays(trialDuration);
                    }
                }

                // Nếu không có Trial thì dùng gói Free
                if (planToAssign == null && !string.IsNullOrEmpty(freePlanName))
                {
                    planToAssign = await _context.Plans
                        .FirstOrDefaultAsync(p => p.PlanName == freePlanName && p.RemovedDate == null);
                }

                if (planToAssign != null)
                {
                    var userPlan = new UserPlan
                    {
                        UserId = user.UserId,
                        PlanId = planToAssign.PlanId,
                        StartDate = TimeHelper.GetVietnamTime(),
                        EndDate = endDate,
                        CreatedDate = TimeHelper.GetVietnamTime(),
                        IsActive = true,
                        RequestInDays = planToAssign.MaxRequests ?? 0
                    };

                    await _context.UserPlans.AddAsync(userPlan);
                    await _context.SaveChangesAsync();
                }
            }
            var accessToken = JwtHelper.GenerateJwtToken(_config, user);
            var refreshToken = JwtHelper.GenerateRefreshToken();

            await _context.UserRefreshTokens.AddAsync(new UserRefreshToken
            {
                UserId = user.UserId,
                RefreshToken = refreshToken,
                DeviceId = model.DeviceId,
                CreatedAt = TimeHelper.GetVietnamTime(),
                ExpiresAt = TimeHelper.GetVietnamTime().AddMonths(1)
            });

            await _context.SaveChangesAsync();
            return (accessToken, refreshToken);
        }

        public async Task<(string accessToken, string refreshToken)> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var token = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(x =>
                    x.RefreshToken == request.RefreshToken &&
                    x.DeviceId == request.DeviceId &&
                    x.ExpiresAt > TimeHelper.GetVietnamTime());

            if (token == null)
                throw new UnauthorizedAccessException("Token không hợp lệ.");

            var user = await _context.Users.FindAsync(token.UserId);
            if (user == null)
                throw new UnauthorizedAccessException("Người dùng không tồn tại.");

            token.RefreshToken = JwtHelper.GenerateRefreshToken();
            token.ExpiresAt = TimeHelper.GetVietnamTime().AddMonths(1);
            await _context.SaveChangesAsync();

            return (JwtHelper.GenerateJwtToken(_config, user), token.RefreshToken);
        }

        public async Task<string> LogoutAsync(string deviceId)
        {
            var token = await _context.UserRefreshTokens.FirstOrDefaultAsync(t => t.DeviceId == deviceId);
            if (token != null)
            {
                _context.UserRefreshTokens.Remove(token);
                await _context.SaveChangesAsync();
                return "Đăng xuất thành công";
            }
            return "Không tìm thấy token.";
        }

        public async Task<SignupResponse> SignupAsync(SignupRequest req)
        {
            var response = new SignupResponse { SignupRequestId = req.SignupRequestId };

            // Check email
            if (string.IsNullOrWhiteSpace(req.Email))
            {
                response.InvalidFields.Add("email");
            }
            else if (!new EmailAddressAttribute().IsValid(req.Email))
            {
                response.InvalidFields.Add("email");
            }
            else if (await _context.Users.AnyAsync(u => u.Email == req.Email))
            {
                response.InvalidFields.Add("email");
            }

            // Check username
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                response.InvalidFields.Add("username");
            }
            else if (await _context.Users.AnyAsync(u => u.UserName.ToLower() == req.Username.ToLower()))
            {
                response.InvalidFields.Add("username");
            }
            if (string.IsNullOrWhiteSpace(req.Password))
            {
                response.InvalidFields.Add("password");
            }
            else if (req.Password != req.ConfirmPassword)
            {
                response.InvalidFields.Add("confirmPassword");
            }

            if (response.InvalidFields.Any()) return response;

            var otp = new SignupOtp
            {
                SignupRequestId = req.SignupRequestId,
                Otpstring = OtpHelper.GenerateRandomDigits(6),
                RequestAttemptsRemains = 3,
                ExpiresAt = TimeHelper.GetVietnamTime().AddMinutes(10)
            };

            await _context.SignupOtps.AddAsync(otp);
            await _context.SaveChangesAsync();

            _ = Task.Run(() => EmailHelper.SendEmailMultiThread(req.Email, "Mã OTP", $"Mã OTP của bạn là <b>{otp.Otpstring}</b>"));

            return response;
        }

        public async Task<ApiResponse<string>> VerifyOtpAsync(string enteredOtp, UserSignupData data)
        {
            if (string.IsNullOrEmpty(enteredOtp))
                return new ApiResponse<string>(400, "OTP không được để trống.");
            var otp = await _context.SignupOtps.FindAsync(data.SignupRequestId);
            if (otp == null)
                return new ApiResponse<string>(401, ErrorMessage.InvalidRequestId);

            if (otp.ExpiresAt < DateTime.Now)
                return new ApiResponse<string>(401, ErrorMessage.ExpiredOTP);

            if (otp.Otpstring != enteredOtp)
            {
                otp.RequestAttemptsRemains--;

                if (otp.RequestAttemptsRemains <= 0)
                {
                    _context.SignupOtps.Remove(otp);
                    await _context.SaveChangesAsync();
                    return new ApiResponse<string>(400, "Bạn đã hết số lần thử OTP.");
                }

                _context.SignupOtps.Update(otp);
                await _context.SaveChangesAsync();
                return new ApiResponse<string>(400, $"OTP không đúng, còn lại {otp.RequestAttemptsRemains} lần thử.");
            }

            var user = new User
            {
                UserName = data.Username,
                Email = data.Email,
                PasswordHash = PasswordHelper.HashPasswordBCrypt(data.Password),
                CreatedDate = TimeHelper.GetVietnamTime(),
                Role = "USER",
                RequestChatbot = 0,
                IsActive = true
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            string? trialPlanName = await _appSettingsService.GetValueAsync("DefaultTrialPlanName");
            string? freePlanName = await _appSettingsService.GetValueAsync("FreePlan");
            int trialDuration = await _appSettingsService.GetIntValueAsync("TrialDurationInDays", 90);

            Plan? planToAssign = null;
            DateTime? endDate = null;

            if (!string.IsNullOrEmpty(trialPlanName))
            {
                planToAssign = await _context.Plans
                    .FirstOrDefaultAsync(p => p.PlanName == trialPlanName && p.RemovedDate == null);

                if (planToAssign != null)
                {
                    endDate = TimeHelper.GetVietnamTime().AddDays(trialDuration);
                }
            }

            // Nếu không có Trial, dùng gói Free
            if (planToAssign == null && !string.IsNullOrEmpty(freePlanName))
            {
                planToAssign = await _context.Plans
                    .FirstOrDefaultAsync(p => p.PlanName == freePlanName && p.RemovedDate == null);
            }

            if (planToAssign != null)
            {
                var userPlan = new UserPlan
                {
                    UserId = user.UserId,
                    PlanId = planToAssign.PlanId,
                    StartDate = TimeHelper.GetVietnamTime(),
                    EndDate = endDate,
                    CreatedDate = TimeHelper.GetVietnamTime(),
                    IsActive = true,
                    RequestInDays = planToAssign.MaxRequests ?? 0
                };

                await _context.UserPlans.AddAsync(userPlan);
            }


            _context.SignupOtps.Remove(otp);
            await _context.SaveChangesAsync();

            return new ApiResponse<string>(201, SuccessMessage.SignupSuccess);
        }

        private async Task DeleteOldRefreshToken(int userId, string deviceId)
        {
            var token = await _context.UserRefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.DeviceId == deviceId);
            if (token != null)
            {
                _context.UserRefreshTokens.Remove(token);
                await _context.SaveChangesAsync();
            }
        }
        public async Task<ApiResponse<string>> SendForgotPasswordOtpAsync(ForgotPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return new ApiResponse<string>(400, "Email không được để trống");

            if (!Regex.IsMatch(req.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return new ApiResponse<string>(400, "Email không đúng định dạng");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null)
                return new ApiResponse<string>(404, "Email chưa được đăng ký");
            // Xoá OTP cũ nếu đã tồn tại
            var existingOtp = await _context.SignupOtps.FindAsync(req.Email);
            if (existingOtp != null)
            {
                _context.SignupOtps.Remove(existingOtp);
                await _context.SaveChangesAsync();
            }
            var otp = new SignupOtp
            {
                SignupRequestId = req.Email,
                Otpstring = OtpHelper.GenerateRandomDigits(6),
                RequestAttemptsRemains = 3,
                ExpiresAt = TimeHelper.GetVietnamTime().AddMinutes(10)
            };

            await _context.SignupOtps.AddAsync(otp);
            await _context.SaveChangesAsync();

            _ = Task.Run(() =>
                EmailHelper.SendEmailMultiThread(req.Email, "Mã OTP đặt lại mật khẩu", $"Mã OTP của bạn là <b>{otp.Otpstring}</b>")
            );

            return new ApiResponse<string>(200, "Mã OTP đã được gửi đến email");
        }
        public async Task<ApiResponse<string>> VerifyForgotPasswordOtpAsync(string enteredOtp, VerifyForgotOtpRequest req)
        {
            var otp = await _context.SignupOtps.FirstOrDefaultAsync(o => o.SignupRequestId == req.Email);
            if (otp == null)
                return new ApiResponse<string>(401, ErrorMessage.InvalidRequestId);

            var now = TimeHelper.GetVietnamTime();
            if (otp.ExpiresAt < now)
            {
                _context.SignupOtps.Remove(otp);
                await _context.SaveChangesAsync();
                return new ApiResponse<string>(401, ErrorMessage.ExpiredOTP);
            }

            if (otp.Otpstring != enteredOtp)
            {
                int remainingAttempts = otp.RequestAttemptsRemains - 1;
                otp.RequestAttemptsRemains = remainingAttempts;

                if (remainingAttempts <= 0)
                {
                    _context.SignupOtps.Remove(otp);
                }
                else
                {
                    _context.SignupOtps.Update(otp);
                }

                await _context.SaveChangesAsync();

                return new ApiResponse<string>(400, $"OTP không đúng, còn lại {remainingAttempts} lần thử.");
            }

            _context.SignupOtps.Remove(otp);
            await _context.SaveChangesAsync();
            return new ApiResponse<string>(200, "OTP hợp lệ, bạn có thể đổi mật khẩu");
        }

        public async Task<ApiResponse<string>> ResetPasswordAsync(ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
                return new ApiResponse<string>(400, "Mật khẩu không được để trống");

        
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == req.Email);
            if (user == null)
                return new ApiResponse<string>(404, "Không tìm thấy tài khoản");

            user.PasswordHash = PasswordHelper.HashPasswordBCrypt(req.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return new ApiResponse<string>(200, "Mật khẩu đã được cập nhật");

        }


    }

}
