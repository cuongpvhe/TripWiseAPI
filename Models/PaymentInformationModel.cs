using System.Text.Json.Serialization;

namespace TripWiseAPI.Models
{
    public class PaymentInformationModel
    {
        [JsonIgnore] // Để Swagger không yêu cầu nhập
        public int UserId { get; set; }

        public string OrderType { get; set; }
        public decimal Amount { get; set; }
        public string OrderDescription { get; set; }
        public string Name { get; set; }
        public string? OrderCode { get; set; }
        public int? TourId { get; set; }
        public int? PlanId { get; set; }
    }
}
