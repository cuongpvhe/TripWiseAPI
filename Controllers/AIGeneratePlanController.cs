using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Model;
using System.Globalization;
using TripWiseAPI.Models;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SimpleChatboxAI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AIGeneratePlanController : ControllerBase
    {
        private readonly VectorSearchService _vectorSearchService;
        private readonly IAiItineraryService _aiService;
        private readonly IAIGeneratePlanService _iAIGeneratePlanService;
        public AIGeneratePlanController(
            VectorSearchService vectorSearchService,
            IAiItineraryService aiService, IAIGeneratePlanService iAIGeneratePlanService)
        {
            _vectorSearchService = vectorSearchService;
            _aiService = aiService;
            _iAIGeneratePlanService = iAIGeneratePlanService;
        }
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
        [HttpPost("CreateItinerary")]
        [ProducesResponseType(typeof(ItineraryResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> CreateItinerary([FromBody] TravelRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
                UserId = parsedId;

            
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
            // Validate user plan (tách vào service)
            var validationResult = await _iAIGeneratePlanService.ValidateAndUpdateUserPlanAsync(UserId.Value);
            if (!validationResult.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = validationResult.ErrorMessage,
                    suggestUpgrade = validationResult.SuggestUpgrade,
                    suggestedPlans = validationResult.SuggestedPlans
                });
            }

            // Generate itinerary using Gemini Service
            try
            {
                var itinerary = await _aiService.GenerateItineraryAsync(request, relatedKnowledge);

                var response = new ItineraryResponse
                {
                    Destination = request.Destination,
                    Days = request.Days,
                    Preferences = request.Preferences,
                    TravelDate = request.TravelDate,
                    Transportation = request.Transportation,
                    DiningStyle = request.DiningStyle,
                    GroupType = request.GroupType,
                    Accommodation = request.Accommodation,
                    TotalEstimatedCost = itinerary.TotalEstimatedCost,
                    Budget = request.BudgetVND,
                    Itinerary = itinerary.Itinerary,
                    SuggestedAccommodation = itinerary.SuggestedAccommodation
                };

                int generatedId = await _iAIGeneratePlanService.SaveGeneratedPlanAsync(UserId.Value, request, response);


                return Ok(new
                {
                    success = true,
                    convertedFromUSD = request.BudgetVND,
                    id = generatedId,
                    data = response
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

        [HttpPost("SaveTourFromGenerated/{generatePlanId}")]
        public async Task<IActionResult> SaveTourFromGenerated(int generatePlanId)
        {
            var result = await _iAIGeneratePlanService.SaveTourFromGeneratedAsync(generatePlanId, GetUserId());
            return Ok(new { success = true, message = "✅ Lưu tour thành công.", data = result });
        }


        [HttpGet("GetToursByUserId")]
        public async Task<IActionResult> GetToursByUserId([FromQuery] int userId)
        {
            var result = await _iAIGeneratePlanService.GetToursByUserIdAsync(userId);
            return Ok(new { message = "✅ Lấy danh sách tour theo user thành công.", data = result });
        }
        


        [HttpGet("GetTourDetailById")]
        public async Task<IActionResult> GetTourDetailById([FromQuery] int tourId)
        {
            var result = await _iAIGeneratePlanService.GetTourDetailByIdAsync(tourId);
            if (result == null)
                return NotFound(new { success = false, message = "❌ Không tìm thấy tour với ID đã cho." });

            return Ok(new { success = true, message = "✅ Lấy chi tiết tour thành công.", data = result });
        }

        [HttpGet("GetHistoryByUser")]
        public async Task<IActionResult> GetHistoryByUser()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _iAIGeneratePlanService.GetHistoryByUserAsync(userId.Value);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        [HttpGet("GetHistoryDetailById/{id}")]
        public async Task<IActionResult> GetHistoryDetailById(int id)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _iAIGeneratePlanService.GetHistoryDetailByIdAsync(id, userId.Value);
            if (result == null)
                return NotFound();

            return Ok(result);
        }


        [HttpDelete("DeleteTour/{id}")]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var userId = GetUserId();
            var result = await _iAIGeneratePlanService.DeleteTourAsync(id, userId);
            if (!result)
                return NotFound(new { success = false, message = "❌ Không tìm thấy tour hoặc không có quyền xoá." });
            return Ok(new { success = true, message = "✅ Tour đã được xoá thành công." });
        }
    }
}
