namespace TripWiseAPI.Models.DTO
{
	public class BlogImageDto
	{
		public int BlogImageID { get; set; }
		public int? ImageID { get; set; }
		public string ImageURL { get; set; }
	}
	public class BlogDto
	{
		public int BlogID { get; set; }
		public string BlogName { get; set; }
		public string BlogContent { get; set; }
		public List<BlogImageDto> BlogImages { get; set; }
	}
	public class CreateBlogDto
	{
		public string BlogName { get; set; }
		public string BlogContent { get; set; }
		//public string CreatedBy { get; set; }
		// Ảnh tải từ máy
		public List<IFormFile> Images { get; set; }

		// Ảnh từ URL bên ngoài (ví dụ: https://abc.com/image.jpg)
		public List<string> ImageUrls { get; set; }
	}
}
