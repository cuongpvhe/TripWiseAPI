using System;
using System.ComponentModel.DataAnnotations;

namespace TripWiseAPI.Model
{
    public class TravelRequest
    {
        [Required(ErrorMessage = "Destination is required")]
        public string Destination { get; set; }

        [Required(ErrorMessage = "TravelDate is required")]
        public DateTime TravelDate { get; set; }

        [Range(1, 30, ErrorMessage = "Days must be between 1 and 30")]
        public int Days { get; set; }

        [Required(ErrorMessage = "Preferences are required")]
        public string Preferences { get; set; }

        [Range(1, double.MaxValue, ErrorMessage = "Budget must be a positive number")]
        public decimal BudgetVND { get; set; }

        public string Transportation { get; set; }
        public string DiningStyle { get; set; }
        public string GroupType { get; set; }
        public string Accommodation { get; set; }
        public int StartDayOffset { get; set; } = 0;

    }
}
