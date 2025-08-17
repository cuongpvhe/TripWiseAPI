using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using TripWiseAPI.Model;
using TripWiseAPI.Models;
using TripWiseAPI.Services;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;

namespace SimpleChatboxAI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AIGeneratePlanController : ControllerBase
    {
        private readonly VectorSearchService _vectorSearchService;
        private readonly IAiItineraryService _aiService;
        private readonly TripWiseDBContext _dbContext;
        private readonly WeatherService _weatherService;
        private readonly IAIGeneratePlanService _iAIGeneratePlanService;
        private readonly IPlanService _iplanService;
        private readonly ITourService _tourService;
		private readonly FirebaseLogService _firebaseLogService;
		public AIGeneratePlanController(
            VectorSearchService vectorSearchService,IAiItineraryService aiService, TripWiseDBContext _context, WeatherService weatherService, IAIGeneratePlanService iAIGeneratePlanService, IPlanService iplanService, ITourService tourService, FirebaseLogService firebaseLogService)
        {
            _vectorSearchService = vectorSearchService;
            _aiService = aiService;
            _dbContext = _context;
            _weatherService = weatherService;
            _iAIGeneratePlanService = iAIGeneratePlanService;
            _iplanService = iplanService;
            _tourService = tourService;
			_firebaseLogService = firebaseLogService;
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

            // Kiểm tra đầu vào
            if (string.IsNullOrWhiteSpace(request.Destination))
                return BadRequest(new { success = false, error = "Vui lòng nhập điểm đến." });

            if (request.TravelDate == default)
                return BadRequest(new { success = false, error = "Vui lòng chọn ngày khởi hành." });

            if (request.TravelDate < DateTime.Today)
                return BadRequest(new { success = false, error = "Ngày khởi hành phải từ hôm nay trở đi." });

            if (request.Days <= 0)
                return BadRequest(new { success = false, error = "Số ngày phải lớn hơn 0." });

            if (string.IsNullOrWhiteSpace(request.Preferences))
                return BadRequest(new { success = false, error = "Vui lòng chọn mục đích chuyến đi." });

            if (request.BudgetVND <= 0)
                return BadRequest(new { success = false, error = "Ngân sách phải là một số dương (VND)." });

            try
            {
                // Vector search để hỗ trợ sinh lịch trình
                string relatedKnowledge = await _vectorSearchService.RetrieveRelevantJsonEntries(
                    request.Destination, 12,
                    request.GroupType ?? "", request.DiningStyle ?? "", request.Preferences ?? "");

                int maxDaysPerChunk = 3;
                int requestedDays = request.Days;

                var chunkRequest = new TravelRequest
                {
                    Destination = request.Destination,
                    TravelDate = request.TravelDate,
                    Days = Math.Min(requestedDays, maxDaysPerChunk),
                    Preferences = request.Preferences,
                    BudgetVND = request.BudgetVND,
                    Transportation = request.Transportation,
                    DiningStyle = request.DiningStyle,
                    GroupType = request.GroupType,
                    Accommodation = request.Accommodation
                };

                var itinerary = await _aiService.GenerateItineraryAsync(chunkRequest, relatedKnowledge);

                // Kiểm tra giới hạn kế hoạch theo plan
                var validationResult = await _iplanService.ValidateAndUpdateUserPlanAsync(UserId.Value, true);
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

                // Gắn thời tiết vào từng ngày
                DateTime startDate = request.TravelDate;
                for (int i = 0; i < itinerary.Itinerary.Count; i++)
                {
                    var day = itinerary.Itinerary[i];
                    var weather = await _weatherService.GetDailyWeatherAsync(request.Destination, startDate.AddDays(i));

                    day.WeatherDescription = weather?.description ?? "Không có dữ liệu";
                    day.TemperatureCelsius = weather?.temperature ?? 0;
                }

                // Địa điểm đã dùng
                var usedPlaces = itinerary.Itinerary
                    .SelectMany(day => day.Activities)
                    .Select(act => act.Address?.Trim())
                    .Where(addr => !string.IsNullOrWhiteSpace(addr))
                    .Distinct()
                    .ToList();

                // Gọi service filter tour có sẵn
                var relatedTours = await _tourService.GetToursByLocationAsync(request.Destination, 4);
                var relatedTourDtos = relatedTours.Select(TourMapper.ToRelatedDto).ToList();
                string? relatedTourMessage = null;
                if (!relatedTourDtos.Any())
                {
                    relatedTourMessage = $"Hiện chưa có tour sẵn nào cho điểm đến {request.Destination}.";
                }

                // Chuẩn bị response
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
                    SuggestedAccommodation = itinerary.SuggestedAccommodation,
                    HasMore = request.Days > maxDaysPerChunk,
                    NextStartDate = request.Days > maxDaysPerChunk ? request.TravelDate.AddDays(maxDaysPerChunk) : null,
                    PreviousAddresses = usedPlaces
                };

                // Lưu kế hoạch
                int generatedId = await _iAIGeneratePlanService.SaveGeneratedPlanAsync(UserId.Value, request, response);
				await _firebaseLogService.LogAsync(userId: UserId ?? 0, action: "Create", message: $"Hành trình cho {request.Destination} đã được tạo thành công với {request.Days} ngày.", statusCode: 200,createdBy:UserId,createdDate:DateTime.Now);
				// Trả về kết quả
				return Ok(new
                {
                    success = true,
                    convertedFromUSD = request.BudgetVND,
                    id = generatedId,
                    data = response,
                    relatedTours = relatedTourDtos, // danh sách tour liên quan
                    relatedTourMessage = relatedTourMessage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Đã xảy ra lỗi trong quá trình tạo lịch trình.",
                    detail = ex.Message
                });
            }
        }

        [HttpPost("GenerateItineraryChunk")]
        [ProducesResponseType(typeof(ItineraryChunkResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GenerateItineraryChunk([FromBody] ItineraryChunkRequest request)
        {
            try
            {
                // Validate
                if (request.BaseRequest == null)
                    return BadRequest(new { success = false, error = "Missing base request" });

                if (request.ChunkSize <= 0 || request.ChunkSize > 3)
                    return BadRequest(new { success = false, error = "Chunk size must be between 1 and 3" });

                // Gọi AI sinh lịch trình tiếp theo
                var result = await _aiService.GenerateChunkAsync(
                    request.BaseRequest,
                    request.StartDate,
                    request.ChunkSize,
                    request.ChunkIndex,
                    request.RelatedKnowledge,
                    request.UsedPlaces
                );

                // Lấy thời tiết cho các ngày mới
                for (int i = 0; i < result.Itinerary.Count; i++)
                {
                    var day = result.Itinerary[i];
                    var weather = await _weatherService.GetDailyWeatherAsync(request.BaseRequest.Destination, request.StartDate.AddDays(i));
                    if (weather != null)
                    {
                        day.WeatherDescription = weather.Value.description;
                        day.TemperatureCelsius = weather.Value.temperature;
                    }
                    else
                    {
                        day.WeatherDescription = "Không có dữ liệu";
                        day.TemperatureCelsius = 0;
                    }
                }

                await _iAIGeneratePlanService.SaveChunkToPlanAsync(request.PlanId, result.Itinerary);

                // Trích xuất địa điểm mới để gửi về client
                var newUsedPlaces = result.Itinerary
                    .SelectMany(day => day.Activities)
                    .Select(act => act.Address?.Trim())
                    .Where(addr => !string.IsNullOrWhiteSpace(addr))
                    .Distinct()
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = result,
                    usedPlaces = newUsedPlaces
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error generating itinerary chunk",
                    detail = ex.Message
                });
            }
        }


        [HttpPost("UpdateItinerary/{generatePlanId}")]
        public async Task<IActionResult> UpdateItinerary(int generatePlanId, [FromBody] ChatUpdateRequest userInput)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized();

            try
            {
                var updated = await _iAIGeneratePlanService.UpdateItineraryAsync(generatePlanId, userId, userInput.Message);

                if (updated == null)
                    return NotFound("Không tìm thấy lịch trình với ID đã cung cấp.");
                
                return Ok(new
                {
                    success = true,
                    message = "Đã cập nhật lịch trình thành công.",
                    data = updated
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi cập nhật lịch trình.",
                    detail = ex.Message
                });
            }
        }

        [HttpPost("UpdateItineraryChunk/{generatePlanId}")]
        public async Task<IActionResult> UpdateItineraryChunk(int generatePlanId, [FromBody] UpdateChunkRequest request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            try
            {
                var updated = await _iAIGeneratePlanService.UpdateItineraryChunkAsync(
                    generatePlanId, userId, request.UserMessage, request.StartDay, request.ChunkSize);

                if (updated == null)
                    return NotFound("Không tìm thấy lịch trình cần cập nhật.");

                return Ok(new
                {
                    success = true,
                    message = "Đã cập nhật một phần lịch trình thành công.",
                    data = updated
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
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
        [HttpDelete("DeleteGenerateTravelPlan/{id}")]
        public async Task<IActionResult> DeleteGenerateTravelPlan(int id)
        {
            var userId = GetUserId();
            var result = await _iAIGeneratePlanService.DeleteGenerateTravelPlansAsync(id, userId);
            if (!result)
                return NotFound(new { success = false, message = "❌ Không tìm thấy tour hoặc không có quyền xoá." });
            return Ok(new { success = true, message = "✅ Tour đã được xoá thành công." });
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
