using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class PaymentTransaction
    {
        public int TransactionId { get; set; }
        public string OrderCode { get; set; } = null!;
        public int? UserId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentStatus { get; set; }
        public string? VnpTransactionNo { get; set; }
        public string? BankCode { get; set; }
        public DateTime? PaymentTime { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
        public int? BookingId { get; set; }
        public int? PlanId { get; set; }

        public virtual Booking? Booking { get; set; }
        public virtual Plan? Plan { get; set; }
        public virtual User? User { get; set; }
    }
}
