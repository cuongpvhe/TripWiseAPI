namespace TripWiseAPI.Models.DTO
{
    public class PaymentTransactionDto
    {
        public int TransactionId { get; set; }
        public string? PlanName { get; set; }
        public int? TourId { get; set; }
        public string? TourName { get; set; }
        public string OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentStatus { get; set; }
        public string? BankCode { get; set; }
        public int? BookingId { get; set; }
        public DateTime? PaymentTime { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
    public class BookingDetailDto
    {
        public int BookingId { get; set; }
        public string TourName { get; set; }
        public string OrderCode { get; set; }
        public DateTime? StartDate { get; set; }
        public string? PaymentStatus { get; set; }
        public string? BankCode { get; set; }
        public string? VnpTransactionNo { get; set; }

        public string UserEmail { get; set; } 
        public string PhoneNumber { get; set; }
        public decimal? PriceAdult { get; set; }
        public decimal? PriceChild5To10 { get; set; }
        public decimal? PriceChildUnder5 { get; set; }
        public decimal Amount { get; set; }
        public DateTime? PaymentTime { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
    public class BookingDto
    {
        public int BookingId { get; set; }
        public int? TourId { get; set; }
        public string TourName { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public decimal TotalAmount { get; set; }
        public string BookingStatus { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
