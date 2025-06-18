using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Review
    {
        public int ReviewId { get; set; }
        public int? UserId { get; set; }
        public int? TourId { get; set; }
        public int? BlogId { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual Tour? Tour { get; set; }
        public virtual User? User { get; set; }
    }
}
