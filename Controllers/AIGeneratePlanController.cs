using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Model;
using System.Globalization;

namespace SimpleChatboxAI.Controllers
{
    [Authorize]
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
        private async Task<int> SaveToGenerateTravelPlanAsync(TravelRequest request, ItineraryResponse response, ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                UserId = parsedId;
            }

            var entity = new GenerateTravelPlan
            {
                ConversationId = Guid.NewGuid().ToString(),
                UserId = UserId,
                TourId = null,
                MessageRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                }),
                MessageResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                }),
                ResponseTime = DateTime.UtcNow
            };

            _dbContext.GenerateTravelPlans.Add(entity);
            await _dbContext.SaveChangesAsync();

            return entity.Id; 
        }


        [HttpPost("SaveTourFromGenerated/{generatePlanId}")]
        public async Task<IActionResult> SaveTourFromGenerated(int generatePlanId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                UserId = parsedId;
            }

            var generatePlan = await _dbContext.GenerateTravelPlans
                .FirstOrDefaultAsync(p => p.Id == generatePlanId);

            if (generatePlan == null || string.IsNullOrEmpty(generatePlan.MessageResponse))
                return NotFound("Không tìm thấy MessageResponse.");

            using var doc = JsonDocument.Parse(generatePlan.MessageResponse);
            var root = doc.RootElement;

            // Lấy dữ liệu từ JSON
            string destination = root.GetProperty("Destination").GetString();
            int days = root.GetProperty("Days").GetInt32();
            string preferences = root.GetProperty("Preferences").GetString();
            string transportation = root.GetProperty("Transportation").GetString();
            string diningStyle = root.GetProperty("DiningStyle").GetString();
            string groupType = root.GetProperty("GroupType").GetString();
            string accommodation = root.GetProperty("Accommodation").GetString();
            DateTime travelDate = root.GetProperty("TravelDate").GetDateTime();
            int totalEstimatedCost = root.GetProperty("TotalEstimatedCost").GetInt32();
            string suggestedAccommodation = root.GetProperty("SuggestedAccommodation").GetString();

            // Tạo tour
            var tour = new Tour
            {
                TourName = $"Tour {destination} - {travelDate:dd/MM/yyyy} - {groupType}",
                Description = $"Chuyến đi {destination} cho {groupType}, ưu tiên {preferences}, di chuyển bằng {transportation}",
                Duration = days.ToString(),
                Price = totalEstimatedCost,
                Location = destination,
                MaxGroupSize = 10,
                Category = preferences,
                TourNote = $"Lưu trú: {accommodation}, Ăn uống: {diningStyle}",
                TourInfo = $"Gợi ý KS: {suggestedAccommodation}",
                TourTypesId = 1,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = UserId
            };

            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();

            var tourAttractions = new List<TourAttraction>();
            var tourItineraries = new List<TourItinerary>();

            var itinerary = root.GetProperty("Itinerary");
            foreach (var day in itinerary.EnumerateArray())
            {
                int dayNumber = day.GetProperty("DayNumber").GetInt32();
                string title = day.GetProperty("Title").GetString();

                foreach (var activity in day.GetProperty("Activities").EnumerateArray())
                {
                    string starttimeStr = activity.GetProperty("starttime").GetString();
                    string endtimeStr = activity.GetProperty("endtime").GetString();
                    TimeSpan starttime = TimeSpan.Parse(starttimeStr);
                    TimeSpan endtime = TimeSpan.Parse(endtimeStr);

                    string description = activity.GetProperty("description").GetString();
                    int estimatedCost = activity.GetProperty("estimatedCost").GetInt32();
                    string address = activity.GetProperty("address").GetString();
                    string placeDetail = activity.GetProperty("placeDetail").GetString();
                    string mapUrl = activity.GetProperty("MapUrl").GetString();
                    string imageUrl = activity.GetProperty("image").GetString();
                    var attraction = new TourAttraction
                    {
                        TourAttractionsName = description,
                        Price = estimatedCost,
                        Localtion = address,
                        Category = preferences,
                        StartTime = starttime,
                        EndTime = endtime,
                        MapUrl = mapUrl,
                        ImageUrl = imageUrl,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = UserId

                    };

                    tourAttractions.Add(attraction);

                    var itineraryItem = new TourItinerary
                    {
                        ItineraryName = title,
                        TourId = tour.TourId,
                        DayNumber = dayNumber,
                        Category = preferences,
                        Description = placeDetail,
                        StartTime = starttime,
                        EndTime = endtime,
                        CreatedDate = DateTime.UtcNow,
                        TourAttractions = attraction,// sẽ gán lại ID sau khi attraction đã lưu
                        CreatedBy = UserId
                    };

                    tourItineraries.Add(itineraryItem);
                }
            }

            _dbContext.TourAttractions.AddRange(tourAttractions);
            await _dbContext.SaveChangesAsync();

            // Gán lại ID từ các attraction vừa thêm
            foreach (var itineraryItem in tourItineraries)
            {
                var matchedAttraction = tourAttractions.FirstOrDefault(a =>
                    a.TourAttractionsName == itineraryItem.TourAttractions.TourAttractionsName &&
                    a.Price == itineraryItem.TourAttractions.Price &&
                    a.Localtion == itineraryItem.TourAttractions.Localtion &&
                    a.StartTime == itineraryItem.TourAttractions.StartTime);

                if (matchedAttraction != null)
                {
                    itineraryItem.TourAttractionsId = matchedAttraction.TourAttractionsId;
                    itineraryItem.TourAttractions = null; // ngăn vòng lặp JSON
                }
            }

            _dbContext.TourItineraries.AddRange(tourItineraries);
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "✅ Đã lưu thành công tour từ MessageResponse.",
                data = new
                {
                    tour.TourId,
                    tour.TourName,
                    tour.Price,
                    tour.Location
                }
            });
        }

        [HttpGet("GetToursByUserId")]
        public async Task<IActionResult> GetToursByUserId([FromQuery] int userId)
        {
            var tours = await _dbContext.Tours
                .Where(t => t.CreatedBy == userId)
                .Select(t => new
                {
                    t.TourId,
                    t.TourName,
                    t.Location,
                    t.Category,
                    t.Price,
                    t.TourNote,
                    t.CreatedDate
                })
                .ToListAsync();

            return Ok(new
            {
                message = "✅ Lấy danh sách tour theo user thành công.",
                data = tours
            });
        }


    }
}
