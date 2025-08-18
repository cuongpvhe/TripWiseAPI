namespace TripWiseAPI.Models.DTO
{
    public class HotNewsDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }
    public class HotNewsRequest
    {
        public IFormFile? ImageFile { get; set; }  // Trường hợp upload file
        public string? ImageUrl { get; set; }      // Trường hợp dùng URL
        public string RedirectUrl { get; set; }    // Link khi click vào ảnh
    }
    public class HotNewsJson
    {
        public int? Id { get; set; }
        public string ImageUrl { get; set; }
        public string RedirectUrl { get; set; }
    }

}
