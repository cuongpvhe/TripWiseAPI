using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class TourItinerary
    {
        public TourItinerary()
        {
            TourAttractions = new HashSet<TourAttraction>();
        }

        public int ItineraryId { get; set; }
        public string? ItineraryName { get; set; }
        public int TourId { get; set; }
        public int? DayNumber { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual Tour Tour { get; set; } = null!;
        public virtual ICollection<TourAttraction> TourAttractions { get; set; }
    }
}
