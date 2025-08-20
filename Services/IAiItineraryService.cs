using TripWiseAPI.Model;

namespace TripWiseAPI.Services
{
    public interface IAiItineraryService
    { 
        Task<ItineraryResponse> GenerateItineraryAsync(TravelRequest request, string relatedKnowledge);
        
        // Method 3 tham số (backward compatibility)
        Task<ItineraryResponse> UpdateItineraryAsync(TravelRequest originalRequest, ItineraryResponse originalResponse, string userInstruction);
        
        // Method 4 tham số (for specific activity update)
        Task<ItineraryResponse> UpdateItineraryAsync(TravelRequest originalRequest, ItineraryResponse originalResponse, string userInstruction, string originalUserMessage);
        
        Task<ItineraryChunkResponse> GenerateChunkAsync(TravelRequest baseRequest, DateTime startDate, int chunkSize, int chunkIndex, string relatedKnowledge, List<string> previousAddresses);
    }
}
