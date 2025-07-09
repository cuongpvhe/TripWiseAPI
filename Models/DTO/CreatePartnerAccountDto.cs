namespace TripWiseAPI.Models.DTO
{
    public class CreatePartnerAccountDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }  
        public string PhoneNumber { get; set; }


        public string CompanyName { get; set; }
        public string? Address { get; set; }
        public string? Website { get; set; }
    }


}
