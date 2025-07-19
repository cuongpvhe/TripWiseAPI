namespace TripWiseAPI.Models.DTO
{
    public class AddImageUrlDto
    {
        public string ImageUrl { get; set; } = null!;
        public string? ImageAlt { get; set; }
    }
    public class TourImageDto
    {
        public int ImageId { get; set; }
        public string? ImageUrl { get; set; }
        public string? ImageAlt { get; set; }
    }

    public class TourListDto
    {
        public int TourId { get; set; }
        public string? TourName { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? CreatedDate { get; set; }
        public List<TourImageDto> Images { get; set; } = new();
    }

}
