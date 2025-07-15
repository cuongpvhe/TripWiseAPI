namespace TripWiseAPI.Models.DTO
{
    public class TourDetailDto
    {
        public string? TourName { get; set; }
        public string? Description { get; set; }
        public DateTime? TravelDate { get; set; }
        public string? Days { get; set; }
        public string? Preferences { get; set; }
        public decimal? Budget { get; set; }
        public decimal? TotalEstimatedCost { get; set; }
        public string? TourInfo { get; set; }
        public string? TourNote { get; set; }
        public string Status { get; set; } = null!;
        public string? RejectReason { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public List<ItineraryDto> Itinerary { get; set; } = new();
    }

    public class ItineraryDto
    {
        public int? DayNumber { get; set; }
        public string? Title { get; set; }
        public decimal? DailyCost { get; set; } // nếu bạn tính từng ngày
        public List<ActivityDto> Activities { get; set; } = new();
    }

    public class ActivityDto
    {
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public decimal? EstimatedCost { get; set; }
        public string? PlaceDetail { get; set; }
        public string? MapUrl { get; set; }
        public string? Image { get; set; }
    }
    public class CreateFullTourDto
    {
        public string TourName { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
        public decimal Price { get; set; }
        public string Location { get; set; }
        public int MaxGroupSize { get; set; }
        public string Category { get; set; }
        public string TourNote { get; set; }
        public string TourInfo { get; set; }
        public int TourTypesID { get; set; }
        public string Status { get; set; }
        public List<ItineraryDayDto> Itinerary { get; set; }
    }
    public class ItineraryDayDto
    {
        public int? DayNumber { get; set; }
        public string Title { get; set; }
        public List<ActivityDto> Activities { get; set; }
    }

    public class ActivityDayDto
    {
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Description { get; set; }
        public decimal? EstimatedCost { get; set; }
        public string? Transportation { get; set; }
        public string? Address { get; set; }
        public string? PlaceDetail { get; set; }
        public string? MapUrl { get; set; }
        public string? Image { get; set; }
    }
    public class PendingTourDto
    {
        public int TourId { get; set; }
        public string? TourName { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
    }
}
