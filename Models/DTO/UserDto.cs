namespace TripWiseAPI.Models.DTO
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string Email { get; set; } = null!;
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        

    }
    public class UserCreateDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!; 
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class UserUpdatelDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = null!;
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? StreetAddress { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
    }
    public class UserDetailDto
    {
        public int UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = null!;
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Avatar { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? StreetAddress { get; set; }
        public bool IsActive { get; set; }
        public int? RequestChatbot { get; set; }
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
        public string? CurrentPlanName { get; set; }
        public DateTime? PlanStartDate { get; set; }
        public DateTime? PlanEndDate { get; set; }
        public int? RemainingRequestInDay { get; set; }

    }

}
