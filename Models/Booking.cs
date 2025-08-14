using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Booking
    {
        public int BookingId { get; set; }
        public string OrderCode { get; set; } = null!;
        public int UserId { get; set; }
        public int TourId { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string? BookingStatus { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
        public DateTime? ExpiredDate { get; set; }
        public int? NumAdults { get; set; }
        public int? NumChildren5To10 { get; set; }
        public int? NumChildrenUnder5 { get; set; }

        public virtual Tour Tour { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
