namespace TripWiseAPI.Models.DTO
{
    public class CreatePartnerAccountDto
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PhoneNumber { get; set; }


        public string CompanyName { get; set; }
        public string? Address { get; set; }
        public string? Website { get; set; }
    }

    public class PartnerDto
    {
        public int PartnerId { get; set; }
        public int UserId { get; set; }
        public string Email { get; set; }
        public string CompanyName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Website { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
    }
    public class PartnerUpdatelDto
    {
        public string Email { get; set; } = null!;
        public string? UserName { get; set; }
        public string CompanyName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Website { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
    }
    public class PartnerDetailDto
    {
        public int PartnerId { get; set; }
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? UserName { get; set; }
        public string CompanyName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string Website { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedByName { get; set; }
        public string? RemovedReason { get; set; }
    }
}
