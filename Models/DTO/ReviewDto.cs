namespace TripWiseAPI.Models.DTO
{
    public class ReviewTourDto
    {

        public int ReviewId { get; set; }
        public string? UserName { get; set; }
        public int? TourId { get; set; } // Tour AI
        public int? UserId { get; set; } // User who is reviewing the tour
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        // Thông tin đối tác
        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }
    }
	public class ReviewTourAIDto
	{
		public int TourId { get; set; }
		public int Rating { get; set; }
		public string? Comment { get; set; }

	}
	public class ReviewTourTourPartnerDto
	{
		public int TourId { get; set; }
		public int Rating { get; set; }
		public string? Comment { get; set; }

	}
	public class ReviewChatbotResponseDto
	{
		public int ReviewId { get; set; }
		public int TourId { get; set; }
		public string TourName { get; set; }
		public int Rating { get; set; }
		public string Comment { get; set; }
		public int? CreatedBy { get; set; }
		public DateTime CreatedAt { get; set; }
		public string TourType { get; set; }     // Tour AI / Tour thường
		public string PartnerName { get; set; }  // Thuộc Partner nào
	}

	public class ReviewResponseDto
	{
		public int ReviewId { get; set; }
		public string? UserName { get; set; }
		public int Rating { get; set; }
		public string? Comment { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
	}
	public class ReviewTourPartnerDto
	{
		public int ReviewId { get; set; }
		public string? UserName { get; set; }
		public int Rating { get; set; }
		public string? Comment { get; set; }
		public string? TourName { get; set; }
		public int? CreatedBy { get; set; }
		public DateTime? CreatedDate { get; set; }
	}
}
