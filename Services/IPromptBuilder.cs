using TripWiseAPI.Model;

namespace TripWiseAPI.Services
{
    public interface IPromptBuilder
    {
        string Build(TravelRequest request, string formattedBudget, string relatedKnowledge);
    }
}
