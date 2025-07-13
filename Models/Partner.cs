using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Partner
    {
        public Partner()
        {
            Tours = new HashSet<Tour>();
        }

        public int PartnerId { get; set; }
        public int UserId { get; set; }
        public string CompanyName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? Website { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual ICollection<Tour> Tours { get; set; }
    }
}
