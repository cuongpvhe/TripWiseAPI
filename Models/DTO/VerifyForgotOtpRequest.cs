namespace TripWiseAPI.Models.DTO
{
    public class VerifyForgotOtpRequest
    {
        public string Email { get; set; }
        
    }

    public class ResendForgotPasswordOtpRequest
    {
        public string Email { get; set; }
    }

}
