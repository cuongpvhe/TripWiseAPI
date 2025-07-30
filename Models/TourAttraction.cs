using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class TourAttraction
    {
        public TourAttraction()
        {
            TourAttractionImages = new HashSet<TourAttractionImage>();
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
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
        public string? MapUrl { get; set; }
        public string? ImageUrl { get; set; }
        public int? ItineraryId { get; set; }
        public string? Description { get; set; }

        public virtual TourItinerary? Itinerary { get; set; }
        public virtual ICollection<TourAttractionImage> TourAttractionImages { get; set; }
    }
}
