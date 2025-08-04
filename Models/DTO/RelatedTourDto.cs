namespace TripWiseAPI.Models.DTO
{
    public class RelatedTourDto
    {
        public int TourId { get; set; }
        public string TourName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public string? Duration { get; set; }
        public string? Location { get; set; }
        public string? Thumbnail { get; set; }
    }

}
