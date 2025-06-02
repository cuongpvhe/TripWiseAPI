namespace TripWiseAPI.Models.DTO
{
    public class SignupRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string SignupRequestId { get; set; }

        public override string ToString()
        {
            return $"Username: {Username}, Email: {Email}, SignupRequestId: {SignupRequestId}";
        }
    }

}
