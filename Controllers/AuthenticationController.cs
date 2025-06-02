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
    [Route("api/[controller]")]
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
        // Login 
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel loginModel)
        {
            // Lấy ra thông tin người dùng đã submit lên API
            if (loginModel != null && loginModel.Username != null && loginModel.Password != null)
            {
                var user = await GetUser(loginModel.Username, loginModel.Password);
                if (user != null)
                {
                    await DeleteOldRefreshToken(user, loginModel.DeviceId);

                    string accessToken = JwtHelper.GenerateJwtToken(_configuration, user);
                    string refreshToken = JwtHelper.GenerateRefreshToken();

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
        // Login with google 
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginModel model)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(model.IdToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] },
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(5)
                });

                // Kiểm tra user đã tồn tại chưa
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
                }

                string accessToken = JwtHelper.GenerateJwtToken(_configuration, user);
                string refreshToken = JwtHelper.GenerateRefreshToken();

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
        // Logout
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

        [HttpPost("verifyOtp/{enteredOtp}")]
        public async Task<ApiResponse<string>> VerifyOtp(string enteredOtp, UserSignupData userSignupData)
        {
            Console.WriteLine("Entered OTP: " + enteredOtp);
            Console.WriteLine("Data: " + userSignupData.ToString());

            // Get this signup request
            var signupRequestOtp = await _context.SignupOtps.FindAsync(userSignupData.SignupRequestId);
            if (signupRequestOtp != null)
            {
                Console.WriteLine("SignupOTP: " + signupRequestOtp.ToString());
                if (signupRequestOtp.Otpstring.Equals(enteredOtp))
                {
                    // Check is this otp expired
                    if (signupRequestOtp.ExpiresAt < DateTime.Now)
                    {
                        return new ApiResponse<string>(401, ErrorMessage.ExpiredOTP);
                    }

                    if (signupRequestOtp.RequestAttemptsRemains <= 0)
                    {
                        await RemoveThisSignupOtp(signupRequestOtp);
                        return new ApiResponse<string>(401, ErrorMessage.OTPValidationFailed);
                    }

                    // Save new user to database
                    var newUser = new User()
                    {
                        UserName = userSignupData.Username,
                        Email = userSignupData.Email,
                        PasswordHash = PasswordHelper.HashPasswordSHA256(userSignupData.Password),
                        CreatedDate = DateTime.UtcNow,
                        Role = "USER",
                        IsActive = true
                    };
                    await _context.Users.AddAsync(newUser);
                    var result = await _context.SaveChangesAsync();
                    if (result > 0)
                    {
                        await RemoveThisSignupOtp(signupRequestOtp);
                        return new ApiResponse<string>(201, SuccessMessage.SignupSuccess);
                    }
                }

                // Get remain attempts
                var remainAttempts = --signupRequestOtp.RequestAttemptsRemains;
                signupRequestOtp.RequestAttemptsRemains = remainAttempts;
                // Update remain attempts
                _context.SignupOtps.Update(signupRequestOtp);
                await _context.SaveChangesAsync();

                if (remainAttempts == 0)
                {
                    await RemoveThisSignupOtp(signupRequestOtp);
                    return new ApiResponse<string>(401, ErrorMessage.OTPValidationFailed);
                }

                return new ApiResponse<string>(400, "Mã OTP không chính xác, bạn còn lại " + remainAttempts + " lần thử.");

            }

            return new ApiResponse<string>(401, ErrorMessage.InvalidRequestId);
        }

        private async Task RemoveThisSignupOtp(SignupOtp signupOtp)
        {
            _context.SignupOtps.Remove(signupOtp);
            await _context.SaveChangesAsync();
        }

        private async Task<User> GetUser(string username, string password)
        {
            return await _context.Users
                .Where(u => u.UserName.ToLower().Equals(username) && u.PasswordHash.Equals(PasswordHelper.HashPasswordSHA256(password)))
                .FirstOrDefaultAsync();
        }

        private async Task<bool> IsThisUsernameExisted(string username) 
        {
            var user = await _context.Users
                .Where(x => x.UserName.ToLower().Equals(username.ToLower()))
                .FirstOrDefaultAsync();
            if (user != null) return true;
            return false;

        }

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
