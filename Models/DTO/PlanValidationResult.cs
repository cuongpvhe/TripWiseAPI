namespace TripWiseAPI.Models.DTO
{
    public class PlanValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public bool SuggestUpgrade { get; set; }
        public List<object>? SuggestedPlans { get; set; }
    }

}
