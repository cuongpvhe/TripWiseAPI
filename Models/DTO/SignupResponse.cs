namespace TripWiseAPI.Models.DTO
{
    public class SignupResponse
    {
        public string SignupRequestId { get; set; }
        public List<string>? InvalidFields { get; set; }
    }
}
