using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Tour
    {
        public Tour()
        {
            Bookings = new HashSet<Booking>();
            GenerateTravelPlans = new HashSet<GenerateTravelPlan>();
            Reviews = new HashSet<Review>();
            TourImages = new HashSet<TourImage>();
            TourItineraries = new HashSet<TourItinerary>();
            Wishlists = new HashSet<Wishlist>();
        }

        public int TourId { get; set; }
        public string TourName { get; set; } = null!;
        public string? Description { get; set; }
        public string Duration { get; set; } = null!;
        public decimal? Price { get; set; }
        public string? Location { get; set; }
        public int? MaxGroupSize { get; set; }
        public string? Category { get; set; }
        public string? TourNote { get; set; }
        public string? TourInfo { get; set; }
        public int? TourTypesId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
        public int? PartnerId { get; set; }
        public string? Status { get; set; }
        public string? RejectReason { get; set; }
        public decimal? PriceAdult { get; set; }
        public decimal? PriceChild5To10 { get; set; }
        public decimal? PriceChildUnder5 { get; set; }
        public DateTime? StartTime { get; set; }
        public int? OriginalTourId { get; set; }

        public virtual Partner? Partner { get; set; }
        public virtual TourType? TourTypes { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<GenerateTravelPlan> GenerateTravelPlans { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<TourImage> TourImages { get; set; }
        public virtual ICollection<TourItinerary> TourItineraries { get; set; }
        public virtual ICollection<Wishlist> Wishlists { get; set; }
    }
}
