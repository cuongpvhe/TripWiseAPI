namespace TripWiseAPI.Models
{
    public class BuyPlanRequest
    {
        public int PlanId { get; set; }
        public string PaymentMethod { get; set; }
    }
    public class BuyTourRequest
    {
        public int TourId { get; set; }
        public string PaymentMethod { get; set; } 
    }


}
