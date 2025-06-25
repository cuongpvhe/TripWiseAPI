using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Partner
    {
        public int PartnerId { get; set; }
        public int UserId { get; set; }
        public string CompanyName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Website { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
