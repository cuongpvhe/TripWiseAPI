using TripWiseAPI.Messages;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers
{
    [ApiController]
    [Route("authentication")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authService;

        public AuthenticationController(IAuthenticationService authService)
        {
            _authService = authService;
        }

        // Đăng nhập bằng email/password
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            (string access, string refresh) = await _authService.LoginAsync(model);
            return Ok(new { AccessToken = access, RefreshToken = refresh });
        }

        // Đăng nhập bằng Google
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginModel model)
        {
            (string access, string refresh) = await _authService.GoogleLoginAsync(model);
            return Ok(new { AccessToken = access, RefreshToken = refresh });
        }

        // Làm mới AccessToken bằng RefreshToken
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            (string access, string refresh) = await _authService.RefreshTokenAsync(request);
            return Ok(new { AccessToken = access, RefreshToken = refresh });
        }

        // Đăng xuất theo deviceId
        [HttpPost("logout/{deviceId}")]
        public async Task<ApiResponse<string>> Logout(string deviceId)
        {
            string message = await _authService.LogoutAsync(deviceId);
            return new ApiResponse<string>(message);
        }

        // Đăng ký (gửi OTP)
        [HttpPost("signup")]
        public async Task<IActionResult> Signup(SignupRequest req)
        {
            var result = await _authService.SignupAsync(req);
            return Ok(result);
        }

        // Xác minh OTP và tạo tài khoản
        [HttpPost("verifyOtp/{otp}")]
        public async Task<ApiResponse<string>> VerifyOtp(string otp, UserSignupData data)
        {
            return await _authService.VerifyOtpAsync(otp, data);
        }
    }
}
