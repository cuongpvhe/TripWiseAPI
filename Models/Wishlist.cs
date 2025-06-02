using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Wishlist
    {
        public int WishlistId { get; set; }
        public int? UserId { get; set; }
        public int? TourId { get; set; }
        public DateTime? DateAdded { get; set; }

        public virtual Tour? Tour { get; set; }
        public virtual User? User { get; set; }
    }
}
