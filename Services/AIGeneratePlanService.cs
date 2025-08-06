using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TripWiseAPI.Model;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Services
{
    public class AIGeneratePlanService : IAIGeneratePlanService
    {
        private readonly TripWiseDBContext _dbContext;
        private readonly WeatherService _weatherService;
        private readonly IAiItineraryService _aiService;
        private readonly ITourService _tourService;


        public AIGeneratePlanService (TripWiseDBContext dbContext, WeatherService weatherService, IAiItineraryService aiService, ITourService tourService)
        {
            _dbContext = dbContext;
            _weatherService = weatherService;
            _aiService = aiService;
            _tourService = tourService;
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

        public async Task<ItineraryResponse?> UpdateItineraryChunkAsync(int generatePlanId, int userId, string userMessage, int startDay, int chunkSize)
        {
            var existingPlan = await _dbContext.GenerateTravelPlans
                .FirstOrDefaultAsync(p => p.Id == generatePlanId && p.UserId == userId);

            if (existingPlan == null) return null;

            var oldRequest = JsonSerializer.Deserialize<TravelRequest>(existingPlan.MessageRequest);
            var oldResponse = JsonSerializer.Deserialize<ItineraryResponse>(existingPlan.MessageResponse);

            if (oldRequest == null || oldResponse == null)
                throw new InvalidOperationException("❌ Dữ liệu gốc bị lỗi, không thể phân tích JSON.");

            // Trích xuất phần lịch trình cần update
            var partialResponse = new ItineraryResponse
            {
                Destination = oldRequest.Destination,
                Days = chunkSize,
                Preferences = oldRequest.Preferences,
                TravelDate = oldRequest.TravelDate.AddDays(startDay - 1),
                Transportation = oldRequest.Transportation,
                DiningStyle = oldRequest.DiningStyle,
                GroupType = oldRequest.GroupType,
                Accommodation = oldRequest.Accommodation,
                TotalEstimatedCost = 0,
                Budget = oldRequest.BudgetVND,
                SuggestedAccommodation = oldResponse.SuggestedAccommodation,
                HasMore = false,
                Itinerary = oldResponse.Itinerary
                    .Where(d => d.DayNumber >= startDay && d.DayNumber < startDay + chunkSize)
                    .ToList()
            };

            // Gọi AI update một phần
            var newChunk = await _aiService.UpdateItineraryAsync(oldRequest, partialResponse, userMessage);

            // Merge phần update vào full lịch trình
            foreach (var updatedDay in newChunk.Itinerary)
            {
                var index = oldResponse.Itinerary.FindIndex(d => d.DayNumber == updatedDay.DayNumber);
                if (index != -1)
                    oldResponse.Itinerary[index] = updatedDay;
            }

            // Cập nhật lại chi phí tổng
            oldResponse.TotalEstimatedCost = oldResponse.Itinerary.Sum(d => d.DailyCost);

            // Gọi weather cho từng ngày
            DateTime startDate = oldRequest.TravelDate;
            for (int i = 0; i < oldResponse.Itinerary.Count; i++)
            {
                var weather = await _weatherService.GetDailyWeatherAsync(oldRequest.Destination, startDate.AddDays(i));
                oldResponse.Itinerary[i].WeatherDescription = weather?.description ?? "Không có dữ liệu";
                oldResponse.Itinerary[i].TemperatureCelsius = weather?.temperature ?? 0;
            }

            // Lưu đè lại
            existingPlan.MessageResponse = JsonSerializer.Serialize(oldResponse, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });
            existingPlan.ResponseTime = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return oldResponse;
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
                ResponseTime = TimeHelper.GetVietnamTime()
        };

            _dbContext.GenerateTravelPlans.Add(entity);
            await _dbContext.SaveChangesAsync();

            return entity.Id;
        }

        public async Task SaveChunkToPlanAsync(int planId, List<ItineraryDay> newDays)
        {
            var plan = await _dbContext.GenerateTravelPlans.FindAsync(planId);
            if (plan == null)
                throw new Exception("Không tìm thấy kế hoạch với ID đã cho");

            // Parse response cũ từ JSON
            var response = JsonSerializer.Deserialize<ItineraryResponse>(plan.MessageResponse);

            if (response == null)
                throw new Exception("Không thể đọc dữ liệu lịch trình hiện tại");

            // Nối thêm các ngày mới
            response.Itinerary.AddRange(newDays);

            // Cập nhật response
            plan.MessageResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });

            plan.ResponseTime = TimeHelper.GetVietnamTime();

            await _dbContext.SaveChangesAsync();
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
                TourName = $"Tour {destination} - {travelDate:dd/MM/yyyy}",
                Description = descriptionBuilder.ToString(),
                Duration = days.ToString(),
                Price = totalEstimatedCost,
                Location = destination,
                MaxGroupSize = 10,
                Category = string.IsNullOrWhiteSpace(preferences) ? "Không rõ" : preferences,
                TourInfo = $"{accommodation}, Phong cách ăn uống: {diningStyle}",
                TourNote = $"{suggestedAccommodation}",
                TourTypesId = 1,
                CreatedBy = userId
            };


            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();

            var tourItineraries = new List<TourItinerary>();
            var tourAttractions = new List<TourAttraction>();

            foreach (var day in root.GetProperty("Itinerary").EnumerateArray())
            {
                int dayNumber = day.GetProperty("DayNumber").GetInt32();
                string title = day.GetProperty("Title").GetString();

                // ✅ Trích xuất thông tin thời tiết nếu có
                string weatherDescription = day.TryGetProperty("WeatherDescription", out var wd) ? wd.GetString() ?? "" : "";
                double? temperatureCelsius = day.TryGetProperty("TemperatureCelsius", out var temp) && temp.ValueKind == JsonValueKind.Number
                    ? temp.GetDouble()
                    : (double?)null;
                string weatherNote = day.TryGetProperty("WeatherNote", out var wn) ? wn.GetString() ?? "" : "";

                // ✅ Tạo mô tả thời tiết nếu có
                string weatherInfo = "";
                if (!string.IsNullOrEmpty(weatherDescription) || temperatureCelsius.HasValue || !string.IsNullOrEmpty(weatherNote))
                {
                    weatherInfo = $"Thời tiết: {weatherDescription}";
                    if (temperatureCelsius.HasValue)
                        weatherInfo += $", {temperatureCelsius.Value}°C";
                    if (!string.IsNullOrEmpty(weatherNote))
                        weatherInfo += $" - {weatherNote}";
                }

                // ✅ Tạo itinerary cho mỗi ngày
                var itinerary = new TourItinerary
                {
                    TourId = tour.TourId,
                    DayNumber = dayNumber,
                    ItineraryName = title,
                    Description = weatherInfo,
                    CreatedBy = userId,
                    CreatedDate = TimeHelper.GetVietnamTime()
                };

                tourItineraries.Add(itinerary);
            }

            // Lưu trước để lấy được ItineraryId
            _dbContext.TourItineraries.AddRange(tourItineraries);
            await _dbContext.SaveChangesAsync();

            // Lặp lại để tạo attraction tương ứng theo Itinerary đã lưu
            int index = 0;
            foreach (var day in root.GetProperty("Itinerary").EnumerateArray())
            {
                var itinerary = tourItineraries[index];
                index++;

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
                        CreatedDate = TimeHelper.GetVietnamTime(),
                        CreatedBy = userId,
                        ItineraryId = itinerary.ItineraryId, // Gán ItineraryId cho attraction
                    };

                    tourAttractions.Add(attraction);
                }
            }

            _dbContext.TourAttractions.AddRange(tourAttractions);
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

            // Tách Destination từ TourName (theo format: "Tour {destination} - {travelDate:dd/MM/yyyy}")
            string destination = null!;
            var parts = tour.TourName?.Split(" - ");
            if (parts != null && parts.Length >= 2 && parts[0].StartsWith("Tour "))
            {
                destination = parts[0].Substring("Tour ".Length).Trim();
            }

            var itineraryByDay = tour.TourItineraries
                .Where(i => i.DayNumber.HasValue)
                .GroupBy(i => i.DayNumber.Value)
                .OrderBy(g => g.Key)
                .Select(g => new ItineraryDetailDto
                {
                    DayNumber = g.Key,
                    Title = g.FirstOrDefault()?.ItineraryName,
                    Description = g.FirstOrDefault()?.Description,
                    DailyCost = g.SelectMany(i => i.TourAttractions).Sum(a => a.Price ?? 0),
                    Activities = g.SelectMany(i => i.TourAttractions.Select(a => new ActivityDetailDto
                    {
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Category = a.Category,
                        Description = a.TourAttractionsName,
                        Address = a.Localtion,
                        EstimatedCost = a.Price ?? 0,
                        MapUrl = a.MapUrl,
                        ImageUrls = a.ImageUrl
                    })).ToList()
                }).ToList();

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

            // 👉 Lấy các tour liên quan theo destination tách từ TourName
            var relatedTours = await _tourService.GetToursByLocationAsync(destination, 4);
            var relatedTourDtos = relatedTours.Select(TourMapper.ToRelatedDto).ToList();

            string? relatedTourMessage = null;
            if (!relatedTourDtos.Any())
            {
                relatedTourMessage = $"Hiện chưa có tour sẵn nào cho điểm đến {destination}.";
            }

            dto.RelatedTours = relatedTourDtos;
            dto.RelatedTourMessage = relatedTourMessage;

            return dto;
        }


        public async Task<bool> DeleteTourAsync(int tourId, int? userId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == userId);

            if (tour == null) return false;

            // Lấy danh sách ItineraryId từ TourItineraries
            var itineraryIds = tour.TourItineraries.Select(i => i.ItineraryId).ToList();

            // Tìm tất cả các TourAttractions có ItineraryId trong danh sách
            var attractionsToDelete = await _dbContext.TourAttractions
                .Where(a => a.ItineraryId != null && itineraryIds.Contains(a.ItineraryId.Value))
                .ToListAsync();

            // Xoá các attractions trước
            _dbContext.TourAttractions.RemoveRange(attractionsToDelete);

            // Sau đó xoá các itinerary
            _dbContext.TourItineraries.RemoveRange(tour.TourItineraries);

            // Cuối cùng xoá tour
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
            // Gọi service filter tour có sẵn
            var relatedTours = await _tourService.GetToursByLocationAsync(response.Destination, 4);
            var relatedTourDtos = relatedTours.Select(TourMapper.ToRelatedDto).ToList();
            string? relatedTourMessage = null;
            if (!relatedTourDtos.Any())
            {
                relatedTourMessage = $"Hiện chưa có tour sẵn nào cho điểm đến {response.Destination}.";
            }
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
                    day.WeatherDescription,
                    day.TemperatureCelsius,
                    day.WeatherNote,
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
                }),
                RelatedTours = relatedTourDtos,
                RelatedTourMessage = relatedTourMessage,
            };
        }
        public async Task<bool> DeleteGenerateTravelPlansAsync(int Id, int? userId)
        {
            var plans = await _dbContext.GenerateTravelPlans
                .Where(p => p.Id == Id && p.UserId == userId)
                .ToListAsync();

            if (!plans.Any())
                return false;

            _dbContext.GenerateTravelPlans.RemoveRange(plans);
            await _dbContext.SaveChangesAsync();

            return true;
        }

    }
}
