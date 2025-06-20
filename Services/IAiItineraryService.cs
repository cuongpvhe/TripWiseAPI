using TripWiseAPI.Model;

namespace TripWiseAPI.Services
{
    public interface IAiItineraryService
    {
        Task<ItineraryResponse> GenerateItineraryAsync(TravelRequest request, string relatedKnowledge);
    }
}
