using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class User
    {
        public User()
        {
            GenerateTravelPlans = new HashSet<GenerateTravelPlan>();
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
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual ICollection<GenerateTravelPlan> GenerateTravelPlans { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<UserPlan> UserPlans { get; set; }
        public virtual ICollection<UserRefreshToken> UserRefreshTokens { get; set; }
        public virtual ICollection<Wishlist> Wishlists { get; set; }
    }
}
