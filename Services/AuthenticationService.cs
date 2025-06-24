using Google.Apis.Auth;
using TripWiseAPI.Messages;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models;
using TripWiseAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace TripWiseAPI.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly TripWiseDBContext _context;
        private readonly IConfiguration _config;

        public AuthenticationService(TripWiseDBContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
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
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMonths(1)
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
                    CreatedDate = DateTime.Now,
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
            }

            var accessToken = JwtHelper.GenerateJwtToken(_config, user);
            var refreshToken = JwtHelper.GenerateRefreshToken();

            await _context.UserRefreshTokens.AddAsync(new UserRefreshToken
            {
                UserId = user.UserId,
                RefreshToken = refreshToken,
                DeviceId = model.DeviceId,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMonths(1)
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
                    x.ExpiresAt > DateTime.UtcNow);

            if (token == null)
                throw new UnauthorizedAccessException("Token không hợp lệ.");

            var user = await _context.Users.FindAsync(token.UserId);
            if (user == null)
                throw new UnauthorizedAccessException("Người dùng không tồn tại.");

            token.RefreshToken = JwtHelper.GenerateRefreshToken();
            token.ExpiresAt = DateTime.UtcNow.AddMonths(1);
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

            if (await _context.Users.AnyAsync(u => u.Email == req.Email))
            {
                response.InvalidFields.Add("email");
            }

            if (await _context.Users.AnyAsync(u => u.UserName.ToLower() == req.Username.ToLower()))
            {
                response.InvalidFields.Add("username");
            }

            if (response.InvalidFields.Any()) return response;

            var otp = new SignupOtp
            {
                SignupRequestId = req.SignupRequestId,
                Otpstring = OtpHelper.GenerateRandomDigits(6),
                RequestAttemptsRemains = 3,
                ExpiresAt = DateTime.Now.AddMinutes(10)
            };

            await _context.SignupOtps.AddAsync(otp);
            await _context.SaveChangesAsync();

            _ = Task.Run(() => EmailHelper.SendEmailMultiThread(req.Email, "Mã OTP", $"Mã OTP của bạn là <b>{otp.Otpstring}</b>"));

            return response;
        }

        public async Task<ApiResponse<string>> VerifyOtpAsync(string enteredOtp, UserSignupData data)
        {
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
                }
                else
                {
                    _context.SignupOtps.Update(otp);
                }
                await _context.SaveChangesAsync();
                return new ApiResponse<string>(400, $"OTP không đúng, còn lại {otp.RequestAttemptsRemains} lần thử.");
            }

            var user = new User
            {
                UserName = data.Username,
                Email = data.Email,
                PasswordHash = PasswordHelper.HashPasswordBCrypt(data.Password),
                CreatedDate = DateTime.UtcNow,
                Role = "USER",
                IsActive = true
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.PlanName == "Free" && p.RemovedDate == null);
            if (freePlan != null)
            {
                var plan = new UserPlan
                {
                    UserId = user.UserId,
                    PlanId = freePlan.PlanId,
                    StartDate = DateTime.UtcNow,
                    CreatedDate = DateTime.UtcNow,
                    IsActive = true
                };
                await _context.UserPlans.AddAsync(plan);
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
                ExpiresAt = DateTime.Now.AddMinutes(10)
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
            var otp = await _context.SignupOtps.FindAsync(req.Email);
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
                }
                else
                {
                    _context.SignupOtps.Update(otp);
                }
                await _context.SaveChangesAsync();
                return new ApiResponse<string>(400, $"OTP không đúng, còn lại {otp.RequestAttemptsRemains} lần thử.");
            }

            // OTP đúng → xóa
            _context.SignupOtps.Remove(otp);
            await _context.SaveChangesAsync();
            return new ApiResponse<string>("OTP hợp lệ, bạn có thể đổi mật khẩu");
        }

        public async Task<ApiResponse<string>> ResetPasswordAsync(ResetPasswordRequest req)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == req.Email);
            if (user == null)
                return new ApiResponse<string>(404, "Không tìm thấy tài khoản");

            user.PasswordHash = PasswordHelper.HashPasswordBCrypt(req.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return new ApiResponse<string>("Mật khẩu đã được cập nhật");
        }


    }

}
