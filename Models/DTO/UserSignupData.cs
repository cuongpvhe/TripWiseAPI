namespace TripWiseAPI.Models.DTO;

public class UserSignupData
{
    public string SignupRequestId { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }

    public override string ToString()
    {
        return $"UserSignupData [SignupRequestId={SignupRequestId}, Username={Username}, Email={Email}]";
    }
}