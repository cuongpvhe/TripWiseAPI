using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class TourAttraction
    {
        public TourAttraction()
        {
            TourAttractionImages = new HashSet<TourAttractionImage>();
            TourItineraries = new HashSet<TourItinerary>();
        }

        public int TourAttractionsId { get; set; }
        public string TourAttractionsName { get; set; } = null!;
        public decimal? Price { get; set; }
        public string? Localtion { get; set; }
        public string? Category { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual ICollection<TourAttractionImage> TourAttractionImages { get; set; }
        public virtual ICollection<TourItinerary> TourItineraries { get; set; }
    }
}
