using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class User
    {
        public User()
        {
            Bookings = new HashSet<Booking>();
            GenerateTravelPlans = new HashSet<GenerateTravelPlan>();
            PaymentTransactions = new HashSet<PaymentTransaction>();
            Reviews = new HashSet<Review>();
            UserPlans = new HashSet<UserPlan>();
            UserRefreshTokens = new HashSet<UserRefreshToken>();
            Wishlists = new HashSet<Wishlist>();
        }

        public int UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Avatar { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Ward { get; set; }
        public string? District { get; set; }
        public string? StreetAddress { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
        public int? RequestChatbot { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual Partner? Partner { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
        public virtual ICollection<GenerateTravelPlan> GenerateTravelPlans { get; set; }
        public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<UserPlan> UserPlans { get; set; }
        public virtual ICollection<UserRefreshToken> UserRefreshTokens { get; set; }
        public virtual ICollection<Wishlist> Wishlists { get; set; }
    }
}
