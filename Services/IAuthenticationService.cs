using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface IAuthenticationService
    {
        Task<(string accessToken, string refreshToken)> LoginAsync(LoginModel model);
        Task<(string accessToken, string refreshToken)> GoogleLoginAsync(GoogleLoginModel model);
        Task<(string accessToken, string refreshToken)> RefreshTokenAsync(RefreshTokenRequest request);
        Task<string> LogoutAsync(string deviceId);
        Task<SignupResponse> SignupAsync(SignupRequest request);
        Task<ApiResponse<string>> VerifyOtpAsync(string enteredOtp, UserSignupData data);
        Task<ApiResponse<string>> SendForgotPasswordOtpAsync(ForgotPasswordRequest req);
        Task<ApiResponse<string>> VerifyForgotPasswordOtpAsync(string enteredOtp, VerifyForgotOtpRequest req);
        Task<ApiResponse<string>> ResetPasswordAsync(ResetPasswordRequest req);
        Task<ApiResponse<string>> ResendSignupOtpAsync(string signupRequestId, string email);
        Task<ApiResponse<string>> ResendForgotPasswordOtpAsync(string email);
    }


}
