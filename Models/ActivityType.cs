using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class ActivityType
    {
        public ActivityType()
        {
            TourItineraries = new HashSet<TourItinerary>();
        }

        public int ActivityTypeId { get; set; }
        public string? ActivityType1 { get; set; }

        public virtual ICollection<TourItinerary> TourItineraries { get; set; }
    }
}
