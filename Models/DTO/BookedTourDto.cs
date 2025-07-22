namespace TripWiseAPI.Models.DTO
{
    public class BookedTourDto
    {
        public int BookingId { get; set; }
        public int TourId { get; set; }
        public string TourName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string? BookingStatus { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
