using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class Plan
    {
        public Plan()
        {
            UserPlans = new HashSet<UserPlan>();
        }

        public int PlanId { get; set; }
        public string? PlanName { get; set; }
        public int? Price { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? RemovedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public int? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
        public string? Description { get; set; }

        public virtual ICollection<UserPlan> UserPlans { get; set; }
    }
}
