using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services.AdminServices
{
    public class ManageTourService : IManageTourService
    {
        private readonly TripWiseDBContext _dbContext;

        public ManageTourService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<List<PendingTourDto>> GetPendingToursAsync()
        {
            var tours = await _dbContext.Tours
                .Where(t => t.Status == TourStatuses.PendingApproval && t.RemovedDate == null)
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    CreatedDate = t.CreatedDate,
                    CreatedBy = t.CreatedBy
                })
                .ToListAsync();

            return tours;
        }
        public async Task<bool> ApproveTourAsync(int tourId, int adminId)
        {
            var tour = await _dbContext.Tours.FindAsync(tourId);
            if (tour == null) return false;

            tour.Status = TourStatuses.Approved;
            tour.ModifiedDate = TimeHelper.GetVietnamTime();
            tour.ModifiedBy = adminId;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectTourAsync(int tourId, string reason, int adminId)
        {
            var tour = await _dbContext.Tours.FindAsync(tourId);
            if (tour == null) return false;

            tour.Status = TourStatuses.Rejected;
            tour.RejectReason = reason;
            tour.ModifiedDate = TimeHelper.GetVietnamTime();
            tour.ModifiedBy = adminId;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<TourDetailDto?> GetTourDetailForAdminAsync(int tourId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.RemovedDate == null);

            if (tour == null) return null;
            async Task<string?> GetUserNameById(int? id)
            {
                if (!id.HasValue) return null;
                return await _dbContext.Users
                    .Where(u => u.UserId == id)
                    .Select(u => u.UserName)
                    .FirstOrDefaultAsync();
            }
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
                CreatedDate = tour.CreatedDate,
                CreatedBy = tour.CreatedBy,
                CreatedByName = await GetUserNameById(tour.CreatedBy),
                ModifiedDate = tour.ModifiedDate,
                ModifiedBy = tour.ModifiedBy,
                ModifiedByName = await GetUserNameById(tour.ModifiedBy)
            };

            return dto;
        }
    
}
}
