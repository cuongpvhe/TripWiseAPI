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
        Task<ApiResponse<string>> VerifyOtpAsync(string otp, UserSignupData data);
    }


}
