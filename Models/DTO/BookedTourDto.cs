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
    public class CancelResultDto
    {
        public int BookingId { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal RefundPercent { get; set; }
        public string? CancelReason { get; set; }
        public string Message { get; set; }
    }
    public class CancelBookingRequest
    {
        public string RefundMethod { get; set; }
        public string CancelReason { get; set; }
    }
    public class RejectRefundRequest
    {
        public string RejectReason { get; set; }
    }
}
