using TripWiseAPI.Model;

namespace TripWiseAPI.Services
{
    public interface IPromptBuilder
    {
        string Build(TravelRequest request, string formattedBudget, string relatedKnowledge, List<string>? previousAddresses = null);
        string BuildUpdatePrompt(TravelRequest request, ItineraryResponse originalResponse, string userInstruction, string relatedKnowledge);
    }
}
