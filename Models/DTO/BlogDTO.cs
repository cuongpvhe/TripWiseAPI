namespace TripWiseAPI.Models.DTO
{
	public class BlogImageDto
	{
		public int BlogImageID { get; set; }
		public int? ImageID { get; set; }
		public string ImageURL { get; set; }
		public string ImageAlt { get; set; }
	}
	public class BlogDto
	{
		public int BlogID { get; set; }
		public string BlogName { get; set; }
		public string BlogContent { get; set; }
		public string? UserName { get; set; }
		public DateTime? CreatedDate { get; set; }
		public int? CreatedBy { get; set; }
		public DateTime? ModifiedDate { get; set; }
		public int? ModifiedBy { get; set; }
		public DateTime? RemovedDate { get; set; }
		public int? RemovedBy { get; set; }
		public List<BlogImageDto> BlogImages { get; set; }
	}
	public class CreateBlogDto
	{
		public string BlogName { get; set; }
		public string BlogContent { get; set; }

		public List<IFormFile>? Images { get; set; }

		// Ảnh từ URL bên ngoài (ví dụ: https://abc.com/image.jpg)
		public List<string>? ImageUrls { get; set; }
	}
}
