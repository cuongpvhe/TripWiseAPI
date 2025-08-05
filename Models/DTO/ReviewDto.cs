namespace TripWiseAPI.Models.DTO
{

	public class ReviewTourAIDto
	{
		public int Rating { get; set; }
		public string? Comment { get; set; }

	}

	public class ReviewResponseDto
	{
		public int ReviewId { get; set; }
		public string? UserName { get; set; }
		public int Rating { get; set; }
		public string? Comment { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
	public class UpdateReviewDto
	{
		public int Rating { get; set; }
		public string? Comment { get; set; }
	}
}
