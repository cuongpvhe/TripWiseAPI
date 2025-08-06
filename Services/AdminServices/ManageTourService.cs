using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Services.AdminServices
{
    public class ManageTourService : IManageTourService
    {
        private readonly TripWiseDBContext _dbContext;

        public ManageTourService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<List<PendingTourDto>> GetToursByStatusAsync(string? status)
        {
            var query = _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Where(t => t.RemovedDate == null && t.TourTypesId == 2);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            return await query
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    StartTime = t.StartTime,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Price = (decimal)t.Price,
                    Status = t.Status,

                    CreatedDate = t.CreatedDate,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList()
                })
                .ToListAsync();
        }
        public async Task<List<PendingTourDto>> GetToursAsync()
        {

            var tours = await _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Where(t => t.Status == TourStatuses.Approved && t.RemovedDate == null)
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    StartTime = t.StartTime,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Price = (decimal)t.Price,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList()
                })
                .ToListAsync();

            return tours;
        }
        public async Task<List<PendingTourDto>> GetRejectToursAsync()
        {

            var tours = await _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Where(t => t.Status == TourStatuses.Rejected && t.RemovedDate == null)
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    StartTime = t.StartTime,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Price = (decimal)t.Price,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList()
                })
                .ToListAsync();

            return tours;
        }
        public async Task<List<PendingTourDto>> GetPendingToursAsync()
        {
            var tours = await _dbContext.Tours
                .Where(t => t.Status == TourStatuses.PendingApproval && t.RemovedDate == null)
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    StartTime = t.StartTime,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Price = (decimal)t.Price,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate,
                    IsUpdatedFromApprovedTour = t.OriginalTourId != null,
                    OriginalTourId = t.OriginalTourId,
                    UpdateNote = t.OriginalTourId != null
                        ? $"Tour này là bản cập nhật của tour đã được duyệt trước đó TourId: ({t.OriginalTourId})"
                        : null
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
        public async Task<bool> PendingTourAsync(int tourId, int adminId)
        {
            var tour = await _dbContext.Tours.FindAsync(tourId);
            if (tour == null) return false;

            tour.Status = TourStatuses.PendingApproval;
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
                .Include(t => t.TourImages)
                    .ThenInclude(ti => ti.Image)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.RemovedDate == null);

            if (tour == null) return null;
            var itineraryDtos = new List<ItineraryDetailDto>();

            foreach (var itinerary in tour.TourItineraries.OrderBy(i => i.DayNumber))
            {
                var attractions = await _dbContext.TourAttractions
                    .Where(a => a.ItineraryId == itinerary.ItineraryId)
                    .Include(a => a.TourAttractionImages)
                        .ThenInclude(ai => ai.Image)
                    .ToListAsync();

                itineraryDtos.Add(new ItineraryDetailDto
                {
                    ItineraryId = itinerary.ItineraryId,
                    DayNumber = itinerary.DayNumber,
                    Title = itinerary.ItineraryName,
                    DailyCost = attractions.Sum(x => x.Price ?? 0),
                    Activities = attractions.Select(a =>
                    {
                        var firstImage = a.TourAttractionImages
                            .Where(ai => ai.Image != null && ai.Image.RemovedDate == null)
                            .Select(ai => ai.Image)
                            .FirstOrDefault();

                        return new ActivityDetailDto
                        {
                            AttractionId = a.TourAttractionsId,
                            StartTime = a.StartTime ?? TimeSpan.Zero,
                            EndTime = a.EndTime ?? TimeSpan.Zero,
                            Description = a.TourAttractionsName,
                            Address = a.Localtion,
                            EstimatedCost = a.Price,
                            PlaceDetail = a.Description,
                            MapUrl = a.MapUrl,
                            ImageUrls = firstImage?.ImageUrl,  // chỉ lấy 1 ảnh duy nhất
                            ImageIds = firstImage?.ImageId.ToString() // ID tương ứng
                        };
                    }).ToList()
                });
            }


            var imageUrls = tour.TourImages
                .Where(ti => ti.Image != null && ti.Image.RemovedDate == null)
                .Select(ti => ti.Image.ImageUrl)
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();

            var imageIds = tour.TourImages
                .Where(ti => ti.Image != null && ti.Image.RemovedDate == null)
                .Select(ti => ti.Image.ImageId.ToString())
                .ToList();

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
                TourId = tour.TourId,
                StartTime = tour.StartTime,
                TourName = tour.TourName,
                Description = tour.Description,
                TravelDate = tour.CreatedDate,
                Days = tour.Duration,
                Location = tour.Location,
                Preferences = tour.Category,
                Budget = null,
                TotalEstimatedCost = tour.Price,
                TourInfo = tour.TourInfo,
                TourNote = tour.TourNote,
                Itinerary = itineraryDtos,
                Status = tour.Status,
                PriceAdult = (decimal)tour.PriceAdult,
                PriceChild5To10 = (decimal)tour.PriceChild5To10,
                PriceChildUnder5 = (decimal)tour.PriceChildUnder5,
                RejectReason = tour.RejectReason,
                ImageUrls = imageUrls,
                ImageIds = imageIds,
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
