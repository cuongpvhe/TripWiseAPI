using TripWiseAPI.Model;

namespace TripWiseAPI.Services
{
    public interface IAiItineraryService
    {
        Task<ItineraryResponse> GenerateItineraryAsync(TravelRequest request, string relatedKnowledge);
        Task<ItineraryResponse> UpdateItineraryAsync(TravelRequest originalRequest, ItineraryResponse originalResponse, string userInstruction);
        Task<ItineraryChunkResponse> GenerateChunkAsync(TravelRequest baseRequest, DateTime startDate, int chunkSize, int chunkIndex, string relatedKnowledge, List<string> previousAddresses);
    }
}
