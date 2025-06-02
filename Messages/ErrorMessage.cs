namespace TripWiseAPI.Messages
{
    public class ErrorMessage
    {
        public const string LoginFailed = "Đăng nhập thất bại";
        public static int RemainsAttempts = 3;
        public static string WrongOTP = $"Mã OTP không chính xác, bạn còn lại {RemainsAttempts} lần thử.";
        public static string OTPValidationFailed = "Bạn đã vượt quá số lần thử xác minh OTP.";
        public const string InvalidRequestId = "Mã yêu cầu không hợp lệ.";
        public const string ExpiredOTP = "Mã OTP đã hết hạn.";
    }
}
