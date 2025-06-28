using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TripWiseAPI.Model;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public class AIGeneratePlanService : IAIGeneratePlanService
    {
        private readonly TripWiseDBContext _dbContext;
        private readonly WeatherService _weatherService;
        private readonly IAiItineraryService _aiService;

        public AIGeneratePlanService (TripWiseDBContext dbContext, WeatherService weatherService, IAiItineraryService aiService)
        {
            _dbContext = dbContext;
            _weatherService = weatherService;
            _aiService = aiService;
        }
        public async Task<ItineraryResponse?> UpdateItineraryAsync(int generatePlanId, int userId, string userMessage)
        {
            var existingPlan = await _dbContext.GenerateTravelPlans
                .FirstOrDefaultAsync(p => p.Id == generatePlanId && p.UserId == userId);

            if (existingPlan == null) return null;

            var oldRequest = JsonSerializer.Deserialize<TravelRequest>(existingPlan.MessageRequest);
            var oldResponse = JsonSerializer.Deserialize<ItineraryResponse>(existingPlan.MessageResponse);

            if (oldRequest == null || oldResponse == null)
                throw new InvalidOperationException("❌ Dữ liệu gốc bị lỗi, không thể phân tích JSON.");

            // Gọi AI để tạo lịch trình mới
            var newItinerary = await _aiService.UpdateItineraryAsync(oldRequest, oldResponse, userMessage);

            // Thêm thông tin thời tiết cho mỗi ngày
            DateTime startDate = oldRequest.TravelDate;
            for (int i = 0; i < newItinerary.Itinerary.Count; i++)
            {
                var weather = await _weatherService.GetDailyWeatherAsync(oldRequest.Destination, startDate.AddDays(i));
                newItinerary.Itinerary[i].WeatherDescription = weather?.description ?? "Không có dữ liệu";
                newItinerary.Itinerary[i].TemperatureCelsius = weather?.temperature ?? 0;
            }

            // Tạo response mới
            var updatedResponse = new ItineraryResponse
            {
                Destination = oldRequest.Destination,
                Days = oldRequest.Days,
                Preferences = oldRequest.Preferences,
                TravelDate = oldRequest.TravelDate,
                Transportation = oldRequest.Transportation,
                DiningStyle = oldRequest.DiningStyle,
                GroupType = oldRequest.GroupType,
                Accommodation = oldRequest.Accommodation,
                TotalEstimatedCost = newItinerary.TotalEstimatedCost,
                Budget = oldRequest.BudgetVND,
                Itinerary = newItinerary.Itinerary,
                SuggestedAccommodation = newItinerary.SuggestedAccommodation
            };

            // Lưu đè kết quả
            existingPlan.MessageResponse = JsonSerializer.Serialize(updatedResponse, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });
            existingPlan.ResponseTime = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return updatedResponse;
        }


        public async Task<int> SaveGeneratedPlanAsync(int? userId, TravelRequest request, ItineraryResponse response)
        {
            var entity = new GenerateTravelPlan
            {
                ConversationId = Guid.NewGuid().ToString(),
                UserId = userId,
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

        public async Task<object> SaveTourFromGeneratedAsync(int generatePlanId, int? userId)
        {
            var generatePlan = await _dbContext.GenerateTravelPlans
                .FirstOrDefaultAsync(p => p.Id == generatePlanId);

            if (generatePlan == null || string.IsNullOrEmpty(generatePlan.MessageResponse))
                throw new Exception("Không tìm thấy lịch trình đã tạo.");

            using var doc = JsonDocument.Parse(generatePlan.MessageResponse);
            var root = doc.RootElement;

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

            var descriptionBuilder = new StringBuilder($"Chuyến đi {destination}");

            if (!string.IsNullOrWhiteSpace(groupType))
                descriptionBuilder.Append($" cho {groupType}");

            if (!string.IsNullOrWhiteSpace(preferences))
                descriptionBuilder.Append($", ưu tiên {preferences}");

            if (!string.IsNullOrWhiteSpace(transportation))
                descriptionBuilder.Append($", di chuyển bằng {transportation}");

            var tour = new Tour
            {
                TourName = $"Tour {destination} - {travelDate:dd/MM/yyyy} - {(string.IsNullOrWhiteSpace(groupType) ? "không rõ nhóm" : groupType)}",
                Description = descriptionBuilder.ToString(),
                Duration = days.ToString(),
                Price = totalEstimatedCost,
                Location = destination,
                MaxGroupSize = 10,
                Category = string.IsNullOrWhiteSpace(preferences) ? "Không rõ" : preferences,
                TourInfo = $"{accommodation}, Phong cách ăn uống: {diningStyle}",
                TourNote = $"{suggestedAccommodation}",
                TourTypesId = 1,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = userId
            };


            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();

            var tourAttractions = new List<TourAttraction>();
            var tourItineraries = new List<TourItinerary>();

            foreach (var day in root.GetProperty("Itinerary").EnumerateArray())
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
                        CreatedBy = userId
                    };

                    tourAttractions.Add(attraction);

                    tourItineraries.Add(new TourItinerary
                    {
                        ItineraryName = title,
                        TourId = tour.TourId,
                        DayNumber = dayNumber,
                        Category = preferences,
                        Description = placeDetail,
                        StartTime = starttime,
                        EndTime = endtime,
                        CreatedDate = DateTime.UtcNow,
                        TourAttractions = attraction,
                        CreatedBy = userId
                    });
                }
            }

            _dbContext.TourAttractions.AddRange(tourAttractions);
            await _dbContext.SaveChangesAsync();

            // Gán lại ID
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

            return new
            {
                tour.TourId,
                tour.TourName,
                tour.Price,
                tour.Location
            };
        }
        public async Task<List<object>> GetToursByUserIdAsync(int userId)
        {
            return await _dbContext.Tours
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
                .Cast<object>()
                .ToListAsync();
        }

        public async Task<TourDetailDto?> GetTourDetailByIdAsync(int tourId)
        {
            var tour = await _dbContext.Tours
                .Where(t => t.TourId == tourId)
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                .FirstOrDefaultAsync();

            if (tour == null) return null;

            var itineraryByDay = tour.TourItineraries
                .Where(i => i.DayNumber.HasValue)
                .GroupBy(i => i.DayNumber.Value)
                .OrderBy(g => g.Key)
                .Select(g => new ItineraryDto
                {
                    DayNumber = g.Key,
                    Title = g.FirstOrDefault()?.ItineraryName,
                    DailyCost = g.Sum(x => x.TourAttractions?.Price ?? 0),
                    Activities = g.Select(i => new ActivityDto
                    {
                        StartTime = i.StartTime,
                        EndTime = i.EndTime,
                        PlaceDetail = i.TourAttractions?.TourAttractionsName,
                        Address = i.TourAttractions?.Localtion,
                        EstimatedCost = i.TourAttractions?.Price ?? 0,
                        Description = i.Description,
                        MapUrl = i.TourAttractions?.MapUrl,
                        Image = i.TourAttractions?.ImageUrl
                    }).ToList()
                })
                .ToList();

            var dto = new TourDetailDto
            {
                TourName = tour.TourName,
                Description = tour.Description,
                TravelDate = tour.CreatedDate,
                Days = tour.Duration,
                Preferences = tour.Category,
                Budget = tour.Price,
                TotalEstimatedCost = itineraryByDay.Sum(d => d.DailyCost ?? 0),
                Itinerary = itineraryByDay,
                TourInfo = tour.TourInfo,
                TourNote = tour.TourNote,
            };

            return dto;
        }


        public async Task<bool> DeleteTourAsync(int tourId, int? userId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == userId);

            if (tour == null) return false;

            var attractionIds = tour.TourItineraries
                .Where(i => i.TourAttractionsId != null)
                .Select(i => i.TourAttractionsId!.Value)
                .Distinct()
                .ToList();

            _dbContext.TourItineraries.RemoveRange(tour.TourItineraries);

            var attractionsToDelete = await _dbContext.TourAttractions
                .Where(a => attractionIds.Contains(a.TourAttractionsId))
                .ToListAsync();

            _dbContext.TourAttractions.RemoveRange(attractionsToDelete);
            _dbContext.Tours.Remove(tour);

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<object>> GetHistoryByUserAsync(int userId)
        {
            var history = await _dbContext.GenerateTravelPlans
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ResponseTime)
                .ToListAsync();

            return history.Select(x =>
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
                } as object;
            }).ToList();
        }

        public async Task<object?> GetHistoryDetailByIdAsync(int id, int userId)
        {
            var entity = await _dbContext.GenerateTravelPlans.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (entity == null) return null;

            var response = JsonSerializer.Deserialize<ItineraryResponse>(entity.MessageResponse);
            return new
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
            };
        }
    }
}
