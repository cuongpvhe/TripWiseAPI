namespace TripWiseAPI.Models.DTO
{
    public class PlanDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = null!;
        public int? Price { get; set; }
        public string? Description { get; set; }
        public int? MaxRequests { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
    }

    public class PlanUserDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = "";
        public decimal? Price { get; set; }
        public string? Description { get; set; }
        public int? MaxRequests { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? EndDate { get; set; } 
    }

    public class PlanCreateDto
    {
        public string PlanName { get; set; } = null!;
        public int? Price { get; set; }
        public string? Description { get; set; }
        public int? MaxRequests { get; set; }
        public int? CreatedBy { get; set; }
    }
    public class PlanUpdateDto
    {
        public string PlanName { get; set; } = null!;
        public int? Price { get; set; }
        public string? Description { get; set; }
        public int? MaxRequests { get; set; }
        public int? ModifiedBy { get; set; }
    }
}
