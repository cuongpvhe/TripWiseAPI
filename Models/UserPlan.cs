using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class UserPlan
    {
        public int UserPlanId { get; set; }
        public int UserId { get; set; }
        public int PlanId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? RequestInDays { get; set; }

        public virtual Plan Plan { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
