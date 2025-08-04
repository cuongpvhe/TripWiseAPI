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
        public int NumAdults { get; set; }
        public int NumChildren5To10 { get; set; }
        public int NumChildrenUnder5 { get; set; }
        public string PaymentMethod { get; set; }
    }



}
