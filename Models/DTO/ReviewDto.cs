namespace TripWiseAPI.Models.DTO
{
	public class ReviewTourDto
	{
		public int? TourId { get; set; } // Tour AI
		public int? UserId { get; set; } // User who is reviewing the tour
		public int Rating { get; set; }
		public string? Comment { get; set; }
		public int? CreatedBy { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
	public class ReviewTourAIDto
	{
		public int? TourId { get; set; } // Tour AI
		public int Rating { get; set; }
		public string? Comment { get; set; }
		public int? CreatedBy { get; set; }
		public DateTime? CreatedDate { get; set; }
	}

	public class ReviewResponseDto
	{
		public int ReviewId { get; set; }
		public string? UserName { get; set; }
		public int Rating { get; set; }
		public string? Comment { get; set; }
		public DateTime? CreatedDate { get; set; }
	}

}
