using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TripWiseAPI.Model;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public class AIGeneratePlanService : IAIGeneratePlanService
    {
        private readonly TripWiseDBContext _dbContext;

        public AIGeneratePlanService (TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<PlanValidationResult> ValidateAndUpdateUserPlanAsync(int userId)
        {
            var userPlan = await _dbContext.UserPlans
                .Include(up => up.Plan)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive == true);

            if (userPlan?.Plan == null)
            {
                return new PlanValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Không tìm thấy gói sử dụng."
                };
            }

            var planName = userPlan.Plan.PlanName;
            if (planName == "Free")
            {
                var nowVN = DateTime.UtcNow.AddHours(7);
                var startOfDayUtc = nowVN.Date.AddHours(-7);
                var endOfDayUtc = nowVN.Date.AddDays(1).AddHours(-7);

                int usageToday = await _dbContext.GenerateTravelPlans
                    .CountAsync(x => x.UserId == userId &&
                                     x.ResponseTime >= startOfDayUtc &&
                                     x.ResponseTime < endOfDayUtc);

                if (usageToday >= 3)
                {
                    var suggestedPlans = await GetSuggestedPlansAsync();
                    return new PlanValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Bạn đã hết 3 lượt miễn phí trong ngày hôm nay.",
                        SuggestUpgrade = true,
                        SuggestedPlans = suggestedPlans
                    };
                }
            }
            else
            {
                if (userPlan.RequestInDays <= 0)
                {
                    var suggestedPlans = await GetSuggestedPlansAsync();
                    return new PlanValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Bạn đã sử dụng hết lượt của gói hiện tại.",
                        SuggestUpgrade = true,
                        SuggestedPlans = suggestedPlans
                    };
                }

                // Trừ lượt
                userPlan.RequestInDays--;
                userPlan.ModifiedDate = DateTime.UtcNow;
                _dbContext.UserPlans.Update(userPlan);
                await _dbContext.SaveChangesAsync();
            }

            return new PlanValidationResult { IsValid = true };
        }

        private async Task<List<object>> GetSuggestedPlansAsync()
        {
            return await _dbContext.Plans
                .Where(p => p.PlanName != "Free" && p.RemovedDate == null)
                .Select(p => new
                {
                    p.PlanId,
                    p.PlanName,
                    p.Price,
                    p.Description
                })
                .Cast<object>()
                .ToListAsync();
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
                        Description = i.Description,
                        Address = i.TourAttractions?.Localtion,
                        Transportation = null,
                        EstimatedCost = i.TourAttractions?.Price ?? 0,
                        PlaceDetail = i.TourAttractions?.TourAttractionsName,
                        MapUrl = i.TourAttractions?.MapUrl,
                        Image = i.TourAttractions?.ImageUrl
                    }).ToList()
                })
                .ToList();

            var dto = new TourDetailDto
            {
                Destination = tour.Location,
                TravelDate = tour.CreatedDate,
                Days = tour.Duration,
                Preferences = tour.TourNote,
                GroupType = tour.Category,
                Budget = tour.Price,
                TotalEstimatedCost = itineraryByDay.Sum(d => d.DailyCost ?? 0),
                Transportation = tour.TourInfo,
                DiningStyle = "",
                Accommodation = "",
                SuggestedAccommodation = "",
                Itinerary = itineraryByDay
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
