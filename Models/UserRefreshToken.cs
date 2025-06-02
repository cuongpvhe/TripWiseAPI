using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class UserRefreshToken
    {
        public int UserRefreshTokenId { get; set; }
        public int UserId { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? DeviceId { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
