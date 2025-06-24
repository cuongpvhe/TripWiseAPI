using TripWiseAPI.Messages;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;


namespace TripWiseAPI.Controllers
{
    [Route("authentication")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private IConfiguration _configuration;
        private readonly TripWiseDBContext _context;
        public AuthenticationController(IConfiguration configuration, TripWiseDBContext context)
        {
            _configuration = configuration;
            _context = context;
        }
        // Handles login with username and password
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel loginModel)
        {
           
            if (loginModel != null && loginModel.Email != null && loginModel.Password != null)
            {
                var user = await GetUser(loginModel.Email, loginModel.Password);
                if (user != null)
                {
                    // Remove any existing refresh token for this device
                    await DeleteOldRefreshToken(user, loginModel.DeviceId);

                    // Generate new access and refresh tokens
                    string accessToken = JwtHelper.GenerateJwtToken(_configuration, user);
                    string refreshToken = JwtHelper.GenerateRefreshToken();

                    // Save refresh token to the database
                    var userToken = new UserRefreshToken()
                    {
                        UserId = user.UserId,
                        RefreshToken = refreshToken,
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddMonths(1),
                        DeviceId = loginModel.DeviceId,
                    };

                    await _context.UserRefreshTokens.AddAsync(userToken);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken
                    });
                }
                else
                {
                    return BadRequest(MessageSender.Error(ErrorMessage.LoginFailed));
                }
            }
            else
            {
                return BadRequest();
            }
        }
        // Handles login using Google authentication
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginModel model)
        {
            try
            {
                // Verify Google idToken
                var payload = await GoogleJsonWebSignature.ValidateAsync(model.IdToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] },
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(5)
                });

                // Check if the user already exists
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
                        PasswordHash = "", 
                    };
                    await _context.Users.AddAsync(user);
                    await _context.SaveChangesAsync();

                    // Send email chào mừng khi tạo tài khoản lần đầu qua Google
                    var welcomeThread = new Thread(() =>
                    {
                        EmailHelper.SendEmailMultiThread(
                            user.Email,
                            "Chào mừng đến với TripWise!",
                            $@"
                                 <div style='font-family: Arial, sans-serif;'>
                                      <h2>Xin chào {user.UserName},</h2>
                                      <p>Chào mừng bạn đến với <strong>TripWise</strong>!</p>
                                      <img src='https://image.slidesdocs.com/responsive-images/slides/vi/0-mau-powerpoint-lap-ke-hoach-du-lich-cho-doanh-nghiep-mau-xanh-lam_9a04cbcec9__960_540.jpg' alt='Welcome Banner' style='max-width:100%; height:auto;' />
                                      <p>Chúng tôi rất vui khi có bạn đồng hành trên mỗi hành trình.</p>
                                      <br/>
                                      <p>Thân ái,<br/>Đội ngũ TripWise</p>
                                 </div>"
                        );


                    });
                    welcomeThread.Start();
                }

                // Generate tokens
                string accessToken = JwtHelper.GenerateJwtToken(_configuration, user);
                string refreshToken = JwtHelper.GenerateRefreshToken();

                // Save refresh token
                var userToken = new UserRefreshToken()
                {
                    UserId = user.UserId,
                    RefreshToken = refreshToken,
                    CreatedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddMonths(1),
                    DeviceId = model.DeviceId
                };

                await _context.UserRefreshTokens.AddAsync(userToken);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                });
            }
            catch (InvalidJwtException ex)
            {
                return Unauthorized("Token không hợp lệ: " + ex.Message);
            }
        }
        // Refresh access token using a valid refresh token
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || string.IsNullOrWhiteSpace(request.DeviceId))
                return BadRequest("Refresh token và deviceId là bắt buộc.");

            // Find refresh token in database
            var userToken = await _context.UserRefreshTokens
                .FirstOrDefaultAsync(x =>
                    x.RefreshToken == request.RefreshToken &&
                    x.DeviceId == request.DeviceId &&
                    x.ExpiresAt > DateTime.UtcNow);

            if (userToken == null)
                return Unauthorized("Refresh token không hợp lệ hoặc đã hết hạn.");

            var user = await _context.Users.FindAsync(userToken.UserId);
            if (user == null)
                return Unauthorized("Người dùng không tồn tại.");

            // Generate new access and refresh tokens
            var newAccessToken = JwtHelper.GenerateJwtToken(_configuration, user);
            var newRefreshToken = JwtHelper.GenerateRefreshToken();

            // Update refresh token in database
            userToken.RefreshToken = newRefreshToken;
            userToken.ExpiresAt = DateTime.UtcNow.AddMonths(1);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }

        // Logout by deleting refresh token of a specific device
        [HttpPost("logout/{deviceId}")]
        public async Task<ApiResponse<string>> Logout(string deviceId)
        {
            var refreshToken = await _context.UserRefreshTokens.Where(urt => urt.DeviceId.Equals(deviceId))
                .FirstOrDefaultAsync();
            if (refreshToken != null)
            {
                _context.Remove(refreshToken);
                int result = await _context.SaveChangesAsync();
                ApiResponse<string> response = new ApiResponse<string>(result < 1 ? "Có lỗi xảy ra" : "Đăng xuất thành công");

                return response;
            }
            return new ApiResponse<string>(404, "Không tìm thấy refresh token cho api này");
        }


        // Delete old refresh token for a user and device
        private async Task DeleteOldRefreshToken(User user, string deviceId)
        {
            var oldTokens =
                await _context.UserRefreshTokens.Where(x => x.UserId == user.UserId && x.DeviceId.Equals(deviceId))
                    .FirstOrDefaultAsync();

            if (oldTokens != null)
            {
                _context.UserRefreshTokens.Remove(oldTokens);
                await _context.SaveChangesAsync();
            }
        }

        // Handles account signup - sends OTP to email
        [HttpPost("signup")]
        public async Task<IActionResult> Signup(SignupRequest signupRequest)
        {
            // Check is valid request data
            if (signupRequest != null
                && signupRequest.Username != null
                && signupRequest.Password != null
                && signupRequest.Email != null
                && signupRequest.Password != null
                && signupRequest.SignupRequestId != null)
            {

                // Create response data with error (if exists)
                var signupResponse = new SignupResponse()
                {
                    SignupRequestId = signupRequest.SignupRequestId
                };

                // Check for existing email
                var invalidFields = new List<string>();
                if (await IsThisEmailExisted(signupRequest.Email))
                {
                    invalidFields.Add("email");
                }

                // Check for existing username
                if (await IsThisUsernameExisted(signupRequest.Username))
                {
                    invalidFields.Add("username");
                }

                // If there are any errors, return them with the invalid fields
                if (invalidFields.Count > 0)
                {
                    signupResponse.InvalidFields = invalidFields;
                    return Ok(signupResponse);
                }

                // If it's ok, generate an otp
                var signupOtp = new SignupOtp()
                {
                    SignupRequestId = signupRequest.SignupRequestId,
                    Otpstring = OtpHelper.GenerateRandomDigits(6),
                    RequestAttemptsRemains = 3,
                    ExpiresAt = DateTime.Now.AddMinutes(10)
                };

                // Save to database
                await _context.SignupOtps.AddAsync(signupOtp);
                await _context.SaveChangesAsync();

                // Send otp to user
                var t = new Thread(() => EmailHelper.SendEmailMultiThread(signupRequest.Email
                    , "Mã OTP xác minh đăng ký tài khoản"
                    , $"Xin chào {signupRequest.Username}, đây là mã OTP xác minh đăng ký tài khoản tại TripWise.VN của bạn: <strong>{signupOtp.Otpstring}</strong>."));
                t.Start();

                return Ok(signupResponse);
            }
            return BadRequest();
        }

        // Verify OTP and create new user account
        [HttpPost("verifyOtp/{enteredOtp}")]
        public async Task<ApiResponse<string>> VerifyOtp(string enteredOtp, UserSignupData userSignupData)
        {
            var signupRequestOtp = await _context.SignupOtps.FindAsync(userSignupData.SignupRequestId);
            if (signupRequestOtp == null)
                return new ApiResponse<string>(401, ErrorMessage.InvalidRequestId);

            // Check OTP khớp
            if (!signupRequestOtp.Otpstring.Equals(enteredOtp))
            {
                signupRequestOtp.RequestAttemptsRemains--;
                _context.SignupOtps.Update(signupRequestOtp);
                await _context.SaveChangesAsync();

                if (signupRequestOtp.RequestAttemptsRemains <= 0)
                {
                    await RemoveThisSignupOtp(signupRequestOtp);
                    return new ApiResponse<string>(401, ErrorMessage.OTPValidationFailed);
                }

                return new ApiResponse<string>(400, $"Mã OTP không chính xác, bạn còn lại {signupRequestOtp.RequestAttemptsRemains} lần thử.");
            }

            // Check OTP hết hạn
            if (signupRequestOtp.ExpiresAt < DateTime.Now)
            {
                return new ApiResponse<string>(401, ErrorMessage.ExpiredOTP);
            }

            // Tạo user
            var newUser = new User()
            {
                UserName = userSignupData.Username,
                Email = userSignupData.Email,
                PasswordHash = PasswordHelper.HashPasswordBCrypt(userSignupData.Password),
                CreatedDate = DateTime.UtcNow,
                Role = "USER",
                IsActive = true
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            // Gán gói Free
            var freePlan = await _context.Plans.FirstOrDefaultAsync(p => p.PlanName == "Free" && p.RemovedDate == null);
            if (freePlan != null)
            {
                var userPlan = new UserPlan
                {
                    UserId = newUser.UserId,
                    PlanId = freePlan.PlanId,
                    StartDate = DateTime.UtcNow,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow,
                    RequestInDays = null
                };

                await _context.UserPlans.AddAsync(userPlan);
                await _context.SaveChangesAsync();
            }

            // Xóa OTP sau khi đăng ký thành công
            await RemoveThisSignupOtp(signupRequestOtp);

            return new ApiResponse<string>(201, SuccessMessage.SignupSuccess);
        }


        // Remove an expired or used OTP from the database
        private async Task RemoveThisSignupOtp(SignupOtp signupOtp)
        {
            _context.SignupOtps.Remove(signupOtp);
            await _context.SaveChangesAsync();
        }

        // Get user from database by username and password
        private async Task<User?> GetUser(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
            if (user != null && PasswordHelper.VerifyPasswordBCrypt(password, user.PasswordHash))
            {
                return user;
            }
            return null;
        }


        // Check if username already exists
        private async Task<bool> IsThisUsernameExisted(string username) 
        {
            var user = await _context.Users
                .Where(x => x.UserName.ToLower().Equals(username.ToLower()))
                .FirstOrDefaultAsync();
            if (user != null) return true;
            return false;

        }

        // Check if email already exists
        private async Task<bool> IsThisEmailExisted(string email)
        {
            var user = await _context.Users
                .Where(x => x.Email.ToLower().Equals(email.ToLower()))
                .FirstOrDefaultAsync();
            if (user != null) return true;
            return false;
        }
    }
}
