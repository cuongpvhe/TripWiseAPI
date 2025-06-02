using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class TourImage
    {
        public int TourImageId { get; set; }
        public int? ImageId { get; set; }
        public int? TourId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual Image? Image { get; set; }
        public virtual Tour? Tour { get; set; }
    }
}
