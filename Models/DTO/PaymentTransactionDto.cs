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
        public DateTime? PaymentTime { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
