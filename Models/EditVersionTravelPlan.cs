using System;
using System.Collections.Generic;

namespace TripWiseAPI.Models
{
    public partial class EditVersionTravelPlan
    {
        public int Id { get; set; }
        public int? GenerateTravelPlanId { get; set; }
        public string? ValueEdited { get; set; }
        public DateTime? EditTime { get; set; }
        public string? ConversationId { get; set; }

        public virtual GenerateTravelPlan? GenerateTravelPlan { get; set; }
    }
}
