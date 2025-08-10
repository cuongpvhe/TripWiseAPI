using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Models.DTO
{
    public class TourDetailDto
    {
        public int TourId { get; set; }
        public DateTime? StartTime { get; set; }
        public string? TourName { get; set; }
        public string? Description { get; set; }
        public string Location { get; set; }
        public DateTime? TravelDate { get; set; }
        public string? Days { get; set; }
        public string? Preferences { get; set; }
        public decimal? Budget { get; set; }
        public decimal PriceAdult { get; set; }
        public decimal PriceChild5To10 { get; set; }
        public decimal PriceChildUnder5 { get; set; }
        public decimal? TotalEstimatedCost { get; set; }
        public string? TourInfo { get; set; }
        public string? TourNote { get; set; }
        public string Status { get; set; } = null!;
        public string? RejectReason { get; set; }
        public int? OriginalTourId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public List<string>? ImageUrls { get; set; }
        public List<string> ImageIds { get; set; }
        public List<ItineraryDetailDto> Itinerary { get; set; } = new();
        public List<RelatedTourDto>? RelatedTours { get; set; }
        public string? RelatedTourMessage { get; set; }
    }

    public class UpdateTourDto
    {
        public DateTime? StartTime { get; set; }
        public string TourName { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
        public decimal? Price { get; set; }
        public decimal PriceAdult { get; set; }
        public decimal PriceChild5To10 { get; set; }
        public decimal PriceChildUnder5 { get; set; }
        public string Location { get; set; }
        public string? Category { get; set; }

    public class ItineraryDto
    {
        public int? DayNumber { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<ActivityDto> Activities { get; set; } = new();
    }
        public class ItineraryDetailDto
        {
            public int ItineraryId { get; set; }
            public int? DayNumber { get; set; }
            public string? Description { get; set; }
            public string? Title { get; set; }
            public decimal? DailyCost { get; set; } // nếu bạn tính từng ngày
            public List<ActivityDetailDto> Activities { get; set; } = new();
        }
        public class ActivityDetailDto
        {
            public int AttractionId { get; set; }
            public TimeSpan? StartTime { get; set; }
            public TimeSpan? EndTime { get; set; }
            public string? Description { get; set; }
            public string? Address { get; set; }
            public decimal? EstimatedCost { get; set; }
            public string? PlaceDetail { get; set; }
            public string? Category { get; set; }
            public string? MapUrl { get; set; }
            public string? ImageIds { get; set; }
            public string? ImageUrls { get; set; }

        }
    public class ActivityDto
    {
            public String? StartTime { get; set; }
            public String? EndTime { get; set; }
            public string? Description { get; set; }
            public decimal? EstimatedCost { get; set; }
            public string? Transportation { get; set; }
            public string? Address { get; set; }
            public string? PlaceDetail { get; set; }
            public string? Category { get; set; }
            public string? MapUrl { get; set; }
            public string? ImageIds { get; set; }
            public string? ImageUrls { get; set; }

        }
    public class CreateTourDto
    {
        public DateTime? StartTime { get; set; }
        public string TourName { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
        //public decimal PriceAdult { get; set; }
        //public decimal PriceChild5To10 { get; set; }
        //public decimal PriceChildUnder5 { get; set; }
        public string Location { get; set; }
        public int MaxGroupSize { get; set; }
        public string? Category { get; set; }
        public string TourNote { get; set; }
        public string TourInfo { get; set; }
        public decimal PriceAdult { get; set; }
        public decimal PriceChild5To10 { get; set; }
        public decimal PriceChildUnder5 { get; set; }
        public List<string>? Image { get; set; }
        public List<IFormFile>? ImageFile { get; set; }
    }
        public class TourDraftDto
        {
            public int TourId { get; set; }
            public DateTime? StartTime { get; set; }
            public string TourName { get; set; }
            public string Description { get; set; }
            public string Duration { get; set; }
            public string Location { get; set; }
            public string MaxGroupSize { get; set; }
            public string? TourNote { get; set; }
            public string? TourInfo { get; set; }
            public string Category { get; set; }
            public decimal? Price { get; set; }
            public string Status { get; set; }
            public decimal PriceAdult { get; set; }
            public decimal PriceChild5To10 { get; set; }
            public decimal PriceChildUnder5 { get; set; }
            public int? TourTypesId { get; set; }
            public int? PartnerID { get; set; }
            public int? OriginalTourId { get; set; }
            public DateTime? CreatedDate { get; set; }
            public int? CreatedBy { get; set; }
            public List<string> TourImages { get; set; } = new();
            public List<ItineraryDto> TourItineraries { get; set; } = new();
        }
        public class CreateItineraryDto
    {
        public int TourId { get; set; }
        public int? DayNumber { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }

        }

    
    public class ActivityDayDto
        {
            public String? StartTime { get; set; }
            public String? EndTime { get; set; }
            public string? Description { get; set; }
            public decimal? EstimatedCost { get; set; }
            public string? Transportation { get; set; }
            public string? Address { get; set; }
            public string? PlaceDetail { get; set; }
            public string? Category { get; set; }
            public string? MapUrl { get; set; }
            public string? Image { get; set; }
            public IFormFile? ImageFile { get; set; }

        }
    public class PendingTourDto
        {
            public int TourId { get; set; }
            public DateTime? StartTime { get; set; }
            public string? TourName { get; set; }
            public string? Description { get; set; }
            public string? Location { get; set; }
            public decimal? Price { get; set; }
            public string Status { get; set; }
            public string? UpdateNote { get; set; }
            public int? PartnerID { get; set; }
            public string? CompanyName { get; set; }

            public bool IsUpdatedFromApprovedTour { get; set; }
            public int? OriginalTourId { get; set; }
            public DateTime? CreatedDate { get; set; }
            public DateTime? ModifiedDate { get; set; }
            public List<string> ImageUrls { get; set; } = new();
        }
    }
}

