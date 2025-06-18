using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Image
    {
        public Image()
        {
            BlogImages = new HashSet<BlogImage>();
            TourAttractionImages = new HashSet<TourAttractionImage>();
            TourImages = new HashSet<TourImage>();
        }

        public int ImageId { get; set; }
        public string? ImageUrl { get; set; }
        public string? ImageAlt { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual ICollection<BlogImage> BlogImages { get; set; }
        public virtual ICollection<TourAttractionImage> TourAttractionImages { get; set; }
        public virtual ICollection<TourImage> TourImages { get; set; }
    }
}
