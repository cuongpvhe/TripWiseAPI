using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class GenerateTravelPlan
    {
        public GenerateTravelPlan()
        {
            EditVersionTravelPlans = new HashSet<EditVersionTravelPlan>();
        }

        public int Id { get; set; }
        public string ConversationId { get; set; } = null!;
        public int? UserId { get; set; }
        public int? TourId { get; set; }
        public string? MessageRequest { get; set; }
        public string? MessageResponse { get; set; }
        public DateTime? ResponseTime { get; set; }

        public virtual Tour? Tour { get; set; }
        public virtual User? User { get; set; }
        public virtual ICollection<EditVersionTravelPlan> EditVersionTravelPlans { get; set; }
    }
}
