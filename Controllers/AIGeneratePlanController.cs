using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Model;
using System.Globalization;

namespace SimpleChatboxAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIGeneratePlanController : ControllerBase
    {
        private readonly VectorSearchService _vectorSearchService;
        private readonly IAiItineraryService _aiService;

        public AIGeneratePlanController(
            VectorSearchService vectorSearchService,
            IAiItineraryService aiService)
        {
            _vectorSearchService = vectorSearchService;
            _aiService = aiService;
        }

        [HttpPost("CreateItinerary")]
        [ProducesResponseType(typeof(ItineraryResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> CreateItinerary([FromBody] TravelRequest request)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { success = false, error = "Destination is required" });

            if (request.TravelDate == default)
                return BadRequest(new { success = false, error = "Travel date is required" });

            if (request.TravelDate < DateTime.Today)
                return BadRequest(new { success = false, error = "Travel date must be today or later" });

            if (request.Days <= 0)
                return BadRequest(new { success = false, error = "Days must be a positive number" });

            if (string.IsNullOrWhiteSpace(request.Preferences))
                return BadRequest(new { success = false, error = "Purpose of trip (Preferences) is required" });

            if (request.BudgetVND <= 0)
                return BadRequest(new { success = false, error = "Budget must be a positive number (VND)" });

            // Call Vector Search
            string relatedKnowledge = await _vectorSearchService.RetrieveRelevantJsonEntries(
                request.Destination, 12,
                request.GroupType ?? "", request.DiningStyle ?? "", request.Preferences ?? "");

            // Generate itinerary using Gemini Service
            try
            {
                var itinerary = await _aiService.GenerateItineraryAsync(request, relatedKnowledge);

                return Ok(new
                {
                    success = true,
                    budgetVND = request.BudgetVND,
                    data = itinerary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "An error occurred while generating the itinerary.",
                    detail = ex.Message
                });
            }
        }
    }
}
