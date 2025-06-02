using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class TourType
    {
        public TourType()
        {
            Tours = new HashSet<Tour>();
        }

        public int TourTypesId { get; set; }
        public string TypeName { get; set; } = null!;

        public virtual ICollection<Tour> Tours { get; set; }
    }
}
