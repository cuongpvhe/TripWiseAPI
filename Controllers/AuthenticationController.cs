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
    /// <summary>
    /// Controller quản lý xác thực người dùng:
    /// đăng nhập, đăng ký, làm mới token, đăng xuất,
    /// quên mật khẩu và đặt lại mật khẩu.
    /// </summary>
    [ApiController]
    [Route("authentication")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        public AuthenticationController(IAuthenticationService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Đăng nhập bằng email và mật khẩu.
        /// </summary>
        /// <param name="model">Thông tin đăng nhập gồm email và mật khẩu.</param>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginModel model)
        {
            (string access, string refresh) = await _authService.LoginAsync(model);
            return Ok(new { AccessToken = access, RefreshToken = refresh });
        }

        /// <summary>
        /// Đăng nhập bằng tài khoản Google.
        /// </summary>
        /// <param name="model">Thông tin đăng nhập Google (token, email,...).</param>
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginModel model)
        {
            (string access, string refresh) = await _authService.GoogleLoginAsync(model);
            return Ok(new { AccessToken = access, RefreshToken = refresh });
        }

        /// <summary>
        /// Làm mới AccessToken bằng RefreshToken.
        /// </summary>
        /// <param name="request">Yêu cầu làm mới token chứa RefreshToken.</param>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            (string access, string refresh) = await _authService.RefreshTokenAsync(request);
            return Ok(new { AccessToken = access, RefreshToken = refresh });
        }

        /// <summary>
        /// Đăng xuất người dùng khỏi hệ thống theo deviceId.
        /// </summary>
        /// <param name="deviceId">ID của thiết bị cần đăng xuất.</param>
        [HttpPost("logout/{deviceId}")]
        public async Task<ApiResponse<string>> Logout(string deviceId)
        {
            string message = await _authService.LogoutAsync(deviceId);
            return new ApiResponse<string>(message);
        }

        /// <summary>
        /// Đăng ký tài khoản mới (gửi mã OTP xác thực).
        /// </summary>
        /// <param name="req">Thông tin đăng ký tài khoản (email, password,...).</param>
        [HttpPost("signup")]
        public async Task<IActionResult> Signup(SignupRequest req)
        {
            var result = await _authService.SignupAsync(req);
            return Ok(result);
        }

        /// <summary>
        /// Xác minh OTP và tạo tài khoản người dùng mới.
        /// </summary>
        /// <param name="enteredOtp">Mã OTP người dùng nhập.</param>
        /// <param name="data">Thông tin đăng ký người dùng kèm theo.</param>
        [HttpPost("verifyOtp/{enteredOtp}")]
        public async Task<ApiResponse<string>> VerifyOtp(string enteredOtp, UserSignupData data)
        {
            return await _authService.VerifyOtpAsync(enteredOtp, data);
        }

        /// <summary>
        /// Yêu cầu đặt lại mật khẩu (gửi OTP đến email).
        /// </summary>
        /// <param name="req">Thông tin email hoặc username của người dùng.</param>
        [HttpPost("forgot-password")]
        public async Task<ApiResponse<string>> ForgotPassword(ForgotPasswordRequest req)
        {
            return await _authService.SendForgotPasswordOtpAsync(req);
        }

        /// <summary>
        /// Xác minh OTP khi quên mật khẩu.
        /// </summary>
        /// <param name="enteredOtp">Mã OTP người dùng nhập.</param>
        /// <param name="req">Thông tin xác thực OTP cho quên mật khẩu.</param>
        [HttpPost("verify-forgot-otp")]
        public async Task<ApiResponse<string>> VerifyForgotOtp(string enteredOtp, VerifyForgotOtpRequest req)
        {
            return await _authService.VerifyForgotPasswordOtpAsync(enteredOtp, req);
        }

        /// <summary>
        /// Đặt lại mật khẩu cho người dùng sau khi xác minh OTP.
        /// </summary>
        /// <param name="req">Yêu cầu đặt lại mật khẩu mới.</param>
        [HttpPost("reset-password")]
        public async Task<ApiResponse<string>> ResetPassword(ResetPasswordRequest req)
        {
            return await _authService.ResetPasswordAsync(req);
        }
        // --- API gửi lại OTP đăng ký ---
        [HttpPost("resend-signup-otp")]
        public async Task<IActionResult> ResendSignupOtp([FromBody] ResendSignupOtpRequest request)
        {
            var result = await _authService.ResendSignupOtpAsync(request.SignupRequestId, request.Email);
            return StatusCode(result.StatusCode, result);
        }

        // --- API gửi lại OTP quên mật khẩu ---
        [HttpPost("resend-forgot-password-otp")]
        public async Task<IActionResult> ResendForgotPasswordOtp([FromBody] ResendForgotPasswordOtpRequest request)
        {
            var result = await _authService.ResendForgotPasswordOtpAsync(request.Email);
            return StatusCode(result.StatusCode, result);
        }
    }
}
