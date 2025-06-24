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
        private readonly TripWiseDBContext _dbContext;
        public AIGeneratePlanController(
            VectorSearchService vectorSearchService,
            IAiItineraryService aiService, TripWiseDBContext _context)
        {
            _vectorSearchService = vectorSearchService;
            _aiService = aiService;
            _dbContext = _context;
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
            // Lấy gói đang dùng
            var userPlan = await _dbContext.UserPlans
                .Include(up => up.Plan)
                .FirstOrDefaultAsync(up => up.UserId == UserId && up.IsActive == true);

            if (userPlan == null || userPlan.Plan == null)
                throw new Exception("Không tìm thấy gói sử dụng.");

            var planName = userPlan.Plan.PlanName;
            var today = DateTime.UtcNow.Date;

            if (planName == "Free")
            {
                var nowVN = DateTime.UtcNow.AddHours(7);
                var startOfDayUtc = nowVN.Date.AddHours(-7);
                var endOfDayUtc = nowVN.Date.AddDays(1).AddHours(-7);

                int usageToday = await _dbContext.GenerateTravelPlans
                    .CountAsync(x => x.UserId == UserId &&
                                     x.ResponseTime >= startOfDayUtc &&
                                     x.ResponseTime < endOfDayUtc);

                if (usageToday >= 3)
                {
                    var availablePlans = await _dbContext.Plans
                        .Where(p => p.PlanName != "Free" && p.RemovedDate == null)
                        .Select(p => new
                        {
                            p.PlanId,
                            p.PlanName,
                            p.Price,
                            p.Description
                        })
                        .ToListAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message = "Bạn đã hết 3 lượt miễn phí trong ngày hôm nay.",
                        suggestUpgrade = true,
                        suggestedPlans = availablePlans
                    });
                }
            }
            else
            {
                if (userPlan.RequestInDays <= 0)
                {
                    var availablePlans = await _dbContext.Plans
                        .Where(p => p.PlanName != "Free" && p.RemovedDate == null)
                        .Select(p => new
                        {
                            p.PlanId,
                            p.PlanName,
                            p.Price,
                            p.Description
                        })
                        .ToListAsync();

                    return BadRequest(new
                    {
                        success = false,
                        message = "Bạn đã sử dụng hết lượt của gói hiện tại.",
                        suggestUpgrade = true,
                        suggestedPlans = availablePlans
                    });
                }

                // Trừ lượt sử dụng
                userPlan.RequestInDays--;
                userPlan.ModifiedDate = DateTime.UtcNow;
                _dbContext.UserPlans.Update(userPlan);
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

                int generatedId = await SaveToGenerateTravelPlanAsync(UserId, request, response);


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
        private async Task<int> SaveToGenerateTravelPlanAsync(int? UserId, TravelRequest request, ItineraryResponse response)

        {

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
                    itineraryItem.TourAttractions = null; 
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
        [HttpGet("GetTourDetailById")]
        public async Task<IActionResult> GetTourDetailById([FromQuery] int tourId)
        {
            var tour = await _dbContext.Tours
                .Where(t => t.TourId == tourId)
                .Select(t => new
                {
                    t.TourId,
                    t.TourName,
                    t.Description,
                    t.Duration,
                    t.Location,
                    t.Price,
                    t.Category,
                    t.TourNote,
                    t.TourInfo,
                    t.CreatedDate,
                    Itineraries = _dbContext.TourItineraries
                        .Where(i => i.TourId == t.TourId)
                        .OrderBy(i => i.DayNumber)
                        .Select(i => new
                        {
                            i.ItineraryId,
                            i.ItineraryName,
                            i.DayNumber,
                            i.StartTime,
                            i.EndTime,
                            i.Description,
                            i.Category,
                            Activities = _dbContext.TourAttractions
                                .Where(a => a.TourAttractionsId == i.TourAttractionsId)
                                .Select(a => new
                                {
                                    a.TourAttractionsId,
                                    a.TourAttractionsName,
                                    a.Price,
                                    a.Localtion,
                                    a.Category,
                                    a.MapUrl,
                                    a.ImageUrl
                                }).FirstOrDefault()
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (tour == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "❌ Không tìm thấy tour với ID đã cho."
                });
            }

            return Ok(new
            {
                success = true,
                message = "✅ Lấy chi tiết tour thành công.",
                data = tour
            });
        }


        [HttpDelete("DeleteTour/{id}")]
        public async Task<IActionResult> DeleteTour(int id)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                UserId = parsedId;
            }


            var tour = await _dbContext.Tours
               .Include(t => t.TourItineraries)
               .FirstOrDefaultAsync(t => t.TourId == id && t.CreatedBy == UserId);

            if (tour == null)
                return NotFound(new { success = false, message = "Tour not found or you don't have permission" });

            // Bước 3: Lấy danh sách TourAttractionsID từ TourItinerary
            var attractionIds = tour.TourItineraries
                .Where(i => i.TourAttractionsId != null)
                .Select(i => i.TourAttractionsId!.Value)
                .Distinct()
                .ToList();

            // Bước 4: Xóa toàn bộ itinerary liên quan
            _dbContext.TourItineraries.RemoveRange(tour.TourItineraries);

            // Bước 5: Xóa các attractions liên kết
            var attractionsToDelete = await _dbContext.TourAttractions
                .Where(a => attractionIds.Contains(a.TourAttractionsId))
                .ToListAsync();
            _dbContext.TourAttractions.RemoveRange(attractionsToDelete);

            // Bước 6: Xóa tour
            _dbContext.Tours.Remove(tour);

            // Bước 7: Lưu thay đổi
            await _dbContext.SaveChangesAsync();

            return Ok(new { success = true, message = "Tour deleted successfully" });
        }


        [HttpGet("GetHistoryByUser")]
        public async Task<IActionResult> GetHistoryByUser()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                UserId = parsedId;
            }

            var history = await _dbContext.GenerateTravelPlans
                .Where(x => x.UserId == UserId)
                .OrderByDescending(x => x.ResponseTime)
                .ToListAsync();

            var result = history.Select(x =>
            {
                var req = JsonSerializer.Deserialize<TravelRequest>(x.MessageRequest);
                return new
                {
                    Id = x.Id,
                    Destination = req.Destination,
                    TravelDate = req.TravelDate,
                    Days = req.Days,
                    Preferences = req.Preferences,
                    BudgetVND = req.BudgetVND,
                    CreatedAt = x.ResponseTime
                };
            });

            return Ok(result);
        }


        [HttpGet("GetHistoryDetailById/{id}")]
        public async Task<IActionResult> GetHistoryDetailById(int id)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            int? UserId = null;

            if (int.TryParse(userIdClaim, out int parsedId))
            {
                UserId = parsedId;
            }

            var entity = await _dbContext.GenerateTravelPlans.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
            if (entity == null)
                return NotFound();

            var response = JsonSerializer.Deserialize<ItineraryResponse>(entity.MessageResponse);
            return Ok(new
            {
                response.Destination,
                response.TravelDate,
                response.Days,
                response.Preferences,
                response.GroupType,
                response.Budget,
                response.TotalEstimatedCost,
                response.Transportation,
                response.DiningStyle,
                response.Accommodation,
                response.SuggestedAccommodation,
                Itinerary = response.Itinerary.Select(day => new
                {
                    Day = day.DayNumber,
                    day.Title,
                    day.DailyCost,
                    Activities = day.Activities.Select(act => new
                    {
                        act.StartTime,
                        act.EndTime,
                        act.Description,
                        act.Address,
                        act.Transportation,
                        act.EstimatedCost,
                        act.PlaceDetail,
                        act.MapUrl,
                        act.Image
                    })
                })
            });

        }


    }
}
