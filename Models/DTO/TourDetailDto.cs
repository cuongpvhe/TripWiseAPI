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

}
