using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services.PartnerServices
{
    public class TourService : ITourService
    {
        private readonly TripWiseDBContext _dbContext;

        public TourService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<List<PendingTourDto>> GetToursByStatusAsync(string? status)
        {
            var query = _dbContext.Tours
                .Where(t => t.RemovedDate == null); 

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            return await query
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate
                })
                .ToListAsync();
        }
        public async Task<int> CreateTourAsync(CreateFullTourDto request, int partnerId)
        {
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == partnerId && p.IsActive == true);

            if (partner == null)
                throw new UnauthorizedAccessException();


            var tour = new Tour
            {
                TourName = request.TourName,
                Description = request.Description,
                Duration = request.Duration,
                Price = request.Price,
                Location = request.Location,
                MaxGroupSize = request.MaxGroupSize,
                Category = request.Category,
                TourNote = request.TourNote,
                TourInfo = request.TourInfo,
                TourTypesId = 2,
                PartnerId = partner.PartnerId,
                Status = request.Status,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = partnerId
            };

            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();

            var attractions = new List<TourAttraction>();
            var itineraries = new List<TourItinerary>();

            foreach (var day in request.Itinerary)
            {
                foreach (var act in day.Activities)
                {
                    var attraction = new TourAttraction
                    {
                        TourAttractionsName = act.Description,
                        Price = act.EstimatedCost,
                        Localtion = act.Address,
                        Category = request.Category,
                        StartTime = act.StartTime,
                        EndTime = act.EndTime,
                        MapUrl = act.MapUrl,
                        ImageUrl = act.Image,
                        CreatedDate = TimeHelper.GetVietnamTime(),
                        CreatedBy = partnerId
                    };

                    attractions.Add(attraction);

                    itineraries.Add(new TourItinerary
                    {
                        ItineraryName = day.Title,
                        TourId = tour.TourId,
                        DayNumber = day.DayNumber,
                        StartTime = attraction.StartTime,
                        EndTime = attraction.EndTime,
                        Description = act.PlaceDetail,
                        Category = request.Category,
                        TourAttractions = attraction,
                        CreatedDate = TimeHelper.GetVietnamTime(),
                        CreatedBy = partnerId
                    });
                }
            }

            _dbContext.TourAttractions.AddRange(attractions);
            _dbContext.TourItineraries.AddRange(itineraries);
            await _dbContext.SaveChangesAsync();

            return tour.TourId;
        }

        public async Task<bool> SubmitTourAsync(int tourId, int partnerId)
        {
            var tour = await _dbContext.Tours.FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == partnerId);
            if (tour == null) return false;

            tour.Status = TourStatuses.PendingApproval;
            tour.RejectReason = null;
            tour.ModifiedDate = TimeHelper.GetVietnamTime();
            tour.ModifiedBy = partnerId;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<TourDetailDto?> GetTourDetailAsync(int tourId, int userId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == userId && t.RemovedDate == null);

            if (tour == null) return null;

            var dto = new TourDetailDto
            {
                TourName = tour.TourName,
                Description = tour.Description,
                TravelDate = tour.CreatedDate,
                Days = tour.Duration,
                Preferences = tour.Category,
                Budget = null,
                TotalEstimatedCost = tour.Price,
                TourInfo = tour.TourInfo,
                TourNote = tour.TourNote,
                Itinerary = tour.TourItineraries
                    .GroupBy(i => i.DayNumber)
                    .Select(g => new ItineraryDto
                    {
                        DayNumber = g.Key,
                        Title = g.FirstOrDefault()?.ItineraryName,
                        DailyCost = g.Sum(x => x.TourAttractions?.Price ?? 0),
                        Activities = g.Select(i => new ActivityDto
                        {
                            StartTime = i.StartTime,
                            EndTime = i.EndTime,
                            Description = i.TourAttractions?.TourAttractionsName,
                            Address = i.TourAttractions?.Localtion,
                            EstimatedCost = i.TourAttractions?.Price,
                            PlaceDetail = i.Description,
                            MapUrl = i.TourAttractions?.MapUrl,
                            Image = i.TourAttractions?.ImageUrl
                        }).ToList()
                    }).ToList(),
                Status = tour.Status,
                RejectReason = tour.RejectReason,
            };

            return dto;
        }
        public async Task<bool> UpdateTourAsync(int tourId, CreateFullTourDto request, int userId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == userId && t.RemovedDate == null);

            if (tour == null) return false;

            if (tour.Status == TourStatuses.Approved)
            {
                // Chuyển sang trạng thái chờ duyệt lại
                tour.Status = TourStatuses.PendingApproval;
                tour.ModifiedBy = userId;
                tour.ModifiedDate = TimeHelper.GetVietnamTime();
                return await _dbContext.SaveChangesAsync() > 0;
            }

            // === Update thông tin Tour ===
            tour.TourName = request.TourName ?? tour.TourName;
            tour.Description = request.Description ?? tour.Description;
            tour.Duration = request.Duration ?? tour.Duration;
            tour.Price = request.Price;
            tour.Location = request.Location ?? tour.Location;
            tour.MaxGroupSize = request.MaxGroupSize;
            tour.Category = request.Category ?? tour.Category;
            tour.TourNote = request.TourNote ?? tour.TourNote;
            tour.TourInfo = request.TourInfo ?? tour.TourInfo;
            tour.Status = TourStatuses.Draft;
            tour.ModifiedDate = TimeHelper.GetVietnamTime();
            tour.ModifiedBy = userId;

            // Lấy các itinerary hiện tại và group theo DayNumber
            var existingItineraries = tour.TourItineraries
                .GroupBy(i => i.DayNumber)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Danh sách các DayNumber gửi lên
            var incomingDayNumbers = request.Itinerary.Select(i => i.DayNumber).ToHashSet();

            // 1. XÓA các itinerary không còn trong request
            var removedDayNumbers = existingItineraries.Keys.Except(incomingDayNumbers);
            foreach (var dayNum in removedDayNumbers)
            {
                var items = existingItineraries[dayNum];
                var attractionIds = items
                    .Where(i => i.TourAttractionsId != null)
                    .Select(i => i.TourAttractionsId.Value)
                    .ToList();

                _dbContext.TourItineraries.RemoveRange(items);

                var attractions = await _dbContext.TourAttractions
                    .Where(a => attractionIds.Contains(a.TourAttractionsId))
                    .ToListAsync();
                _dbContext.TourAttractions.RemoveRange(attractions);
            }

            // 2. THÊM MỚI hoặc CẬP NHẬT itinerary/activity
            foreach (var dayDto in request.Itinerary)
            {
                var dayNumber = dayDto.DayNumber;
                var title = dayDto.Title;

                // Nếu chưa có DayNumber này thì thêm mới tất cả activity
                if (!existingItineraries.TryGetValue(dayNumber, out var dayItineraries))
                {
                    foreach (var act in dayDto.Activities)
                    {
                        var newAttraction = new TourAttraction
                        {
                            TourAttractionsName = act.Description,
                            Price = act.EstimatedCost ?? 0,
                            Localtion = act.Address,
                            Category = request.Category,
                            StartTime = act.StartTime,
                            EndTime = act.EndTime,
                            MapUrl = act.MapUrl,
                            ImageUrl = act.Image,
                            CreatedDate = TimeHelper.GetVietnamTime(),
                            CreatedBy = userId
                        };

                        var newItinerary = new TourItinerary
                        {
                            ItineraryName = title,
                            TourId = tour.TourId,
                            DayNumber = dayNumber,
                            StartTime = act.StartTime,
                            EndTime = act.EndTime,
                            Description = act.PlaceDetail,
                            Category = request.Category,
                            TourAttractions = newAttraction,
                            CreatedDate = TimeHelper.GetVietnamTime(),
                            CreatedBy = userId
                        };

                        _dbContext.TourAttractions.Add(newAttraction);
                        _dbContext.TourItineraries.Add(newItinerary);
                    }
                }
                else
                {
                    // DayNumber đã tồn tại → cập nhật hoặc thêm mới activity
                    var existingActivities = dayItineraries.ToList();

                    // Cập nhật hoạt động cũ nếu còn
                    for (int i = 0; i < dayDto.Activities.Count; i++)
                    {
                        var act = dayDto.Activities[i];
                        var itinerary = existingActivities.ElementAtOrDefault(i);

                        if (itinerary != null && itinerary.TourAttractions != null)
                        {
                            var attraction = itinerary.TourAttractions;
                            itinerary.ItineraryName = title ?? itinerary.ItineraryName;
                            itinerary.Description = act.PlaceDetail ?? itinerary.Description;
                            itinerary.StartTime = act.StartTime ?? itinerary.StartTime;
                            itinerary.EndTime = act.EndTime ?? itinerary.EndTime;
                            itinerary.ModifiedBy = userId;
                            itinerary.ModifiedDate = TimeHelper.GetVietnamTime();

                            attraction.TourAttractionsName = act.Description ?? attraction.TourAttractionsName;
                            attraction.Price = act.EstimatedCost ?? attraction.Price;
                            attraction.Localtion = act.Address ?? attraction.Localtion;
                            attraction.StartTime = act.StartTime ?? attraction.StartTime;
                            attraction.EndTime = act.EndTime ?? attraction.EndTime;
                            attraction.MapUrl = act.MapUrl ?? attraction.MapUrl;
                            attraction.ImageUrl = act.Image ?? attraction.ImageUrl;
                            attraction.ModifiedBy = userId;
                            attraction.ModifiedDate = TimeHelper.GetVietnamTime();
                        }
                        else
                        {
                            // Thêm mới activity nếu chưa tồn tại
                            var newAttraction = new TourAttraction
                            {
                                TourAttractionsName = act.Description,
                                Price = act.EstimatedCost ?? 0,
                                Localtion = act.Address,
                                Category = request.Category,
                                StartTime = act.StartTime,
                                EndTime = act.EndTime,
                                MapUrl = act.MapUrl,
                                ImageUrl = act.Image,
                                CreatedDate = TimeHelper.GetVietnamTime(),
                                CreatedBy = userId
                            };

                            var newItinerary = new TourItinerary
                            {
                                ItineraryName = title,
                                TourId = tour.TourId,
                                DayNumber = dayNumber,
                                StartTime = act.StartTime,
                                EndTime = act.EndTime,
                                Description = act.PlaceDetail,
                                Category = request.Category,
                                TourAttractions = newAttraction,
                                CreatedDate = TimeHelper.GetVietnamTime(),
                                CreatedBy = userId
                            };

                            _dbContext.TourAttractions.Add(newAttraction);
                            _dbContext.TourItineraries.Add(newItinerary);
                        }
                    }

                    // Nếu có ít hoạt động hơn request → xoá phần thừa
                    if (existingActivities.Count > dayDto.Activities.Count)
                    {
                        var extraItineraries = existingActivities.Skip(dayDto.Activities.Count).ToList();
                        var attractionIdsToDelete = extraItineraries
                            .Where(i => i.TourAttractionsId != null)
                            .Select(i => i.TourAttractionsId.Value)
                            .ToList();

                        _dbContext.TourItineraries.RemoveRange(extraItineraries);

                        var extraAttractions = await _dbContext.TourAttractions
                            .Where(a => attractionIdsToDelete.Contains(a.TourAttractionsId))
                            .ToListAsync();

                        _dbContext.TourAttractions.RemoveRange(extraAttractions);
                    }
                }
            }

            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteOrDraftTourAsync(int tourId, string action, int partnerId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == partnerId);

            if (tour == null) return false;

            if (action == "delete")
            {
                var attractionIds = tour.TourItineraries
                    .Where(i => i.TourAttractionsId != null)
                    .Select(i => i.TourAttractionsId!.Value)
                    .Distinct()
                    .ToList();

                // Xoá lịch trình trước
                _dbContext.TourItineraries.RemoveRange(tour.TourItineraries);

                // Xoá attraction nếu không còn liên kết
                var attractionsToDelete = await _dbContext.TourAttractions
                    .Where(a => attractionIds.Contains(a.TourAttractionsId))
                    .ToListAsync();

                _dbContext.TourAttractions.RemoveRange(attractionsToDelete);

                // Cuối cùng xoá tour
                _dbContext.Tours.Remove(tour);
            }
            else if (action == "to_draft")
            {
                tour.Status = TourStatuses.Draft;
                tour.RejectReason = null;
                tour.ModifiedDate = TimeHelper.GetVietnamTime();
                tour.ModifiedBy = partnerId;
            }
            else return false;

            await _dbContext.SaveChangesAsync();
            return true;
        }

    }
    public static class TourStatuses
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }
}
