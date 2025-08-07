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
        private readonly IImageUploadService _imageUploadService;

        public ManageTourService(TripWiseDBContext dbContext, IImageUploadService imageUploadService)
        {
            _dbContext = dbContext;
            _imageUploadService = imageUploadService;
        }
        public async Task<List<PendingTourDto>> GetToursByStatusAsync(string? status = null, int? partnerId = null, int? day = null, int? month = null, int? year = null)
        {
            var query = _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Where(t => t.RemovedDate == null && t.TourTypesId == 2);

            // Lọc theo status và partnerId nếu có
            if (!string.IsNullOrEmpty(status) && partnerId.HasValue)
            {
                query = query.Where(t => t.Status == status && t.PartnerId == partnerId.Value);
            }
            else if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }
            else if (partnerId.HasValue)
            {
                query = query.Where(t => t.PartnerId == partnerId.Value);
            }

            // ------------------------------
            // Filter theo ModifiedDate nếu có
            if (year.HasValue)
            {
                query = query.Where(t => t.ModifiedDate.HasValue && t.ModifiedDate.Value.Year == year.Value);
            }
            if (month.HasValue)
            {
                query = query.Where(t => t.ModifiedDate.HasValue && t.ModifiedDate.Value.Month == month.Value);
            }
            if (day.HasValue)
            {
                query = query.Where(t => t.ModifiedDate.HasValue && t.ModifiedDate.Value.Day == day.Value);
            }
            // ------------------------------

            var tours = await query
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    StartTime = t.StartTime,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Price = (decimal)t.PriceAdult,
                    Status = t.Status,
                    PartnerID = t.PartnerId,
                    CreatedDate = t.CreatedDate,
                    ModifiedDate = t.ModifiedDate,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList(),

                    OriginalTourId = t.Status == TourStatuses.PendingApproval ? t.OriginalTourId : null,
                    IsUpdatedFromApprovedTour = t.Status == TourStatuses.PendingApproval && t.OriginalTourId != null,
                    UpdateNote = t.Status == TourStatuses.PendingApproval && t.OriginalTourId != null
                        ? $"Tour này là bản cập nhật của tour đã được duyệt trước đó TourId: ({t.OriginalTourId})"
                        : null
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
                    Price = (decimal)t.PriceAdult,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList()
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
                TotalEstimatedCost = tour.PriceAdult,
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
                OriginalTourId = tour.OriginalTourId,
                CreatedDate = tour.CreatedDate,
                CreatedBy = tour.CreatedBy,
                CreatedByName = await GetUserNameById(tour.CreatedBy),
                ModifiedDate = tour.ModifiedDate,
                ModifiedBy = tour.ModifiedBy,
                ModifiedByName = await GetUserNameById(tour.ModifiedBy)
            };

            return dto;
        }
        public async Task SubmitDraftAsync(int tourId, int adminId)
        {
            // Lấy bản nháp
            var draft = await _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                        .ThenInclude(a => a.TourAttractionImages)
                            .ThenInclude(ai => ai.Image)
                .FirstOrDefaultAsync(t => t.OriginalTourId == tourId && t.Status == TourStatuses.PendingApproval);

            // Lấy tour gốc
            var original = await _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                        .ThenInclude(a => a.TourAttractionImages)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.Status == TourStatuses.Approved);

            if (draft == null || original == null)
                throw new Exception("Draft or original tour not found");

            // ===============================
            // Cập nhật thông tin cơ bản
            // ===============================
            original.TourName = draft.TourName;
            original.Description = draft.Description;
            original.Duration = draft.Duration;
            original.StartTime = draft.StartTime;
            original.MaxGroupSize = draft.MaxGroupSize;
            original.TourNote = draft.TourNote;
            original.TourInfo = draft.TourInfo;
            original.Category = draft.Category;
            original.Location = draft.Location;
            original.PriceAdult = draft.PriceAdult;
            original.PriceChild5To10 = draft.PriceChild5To10;
            original.PriceChildUnder5 = draft.PriceChildUnder5;
            original.TourTypesId = draft.TourTypesId;
            original.ModifiedDate = TimeHelper.GetVietnamTime();

            // ===============================
            // Cập nhật hình ảnh
            // ===============================
            _dbContext.TourImages.RemoveRange(original.TourImages);

            foreach (var draftImage in draft.TourImages)
            {
                var image = new Image { ImageUrl = draftImage.Image.ImageUrl };
                _dbContext.Images.Add(image);

                original.TourImages.Add(new TourImage
                {
                    Image = image
                });
            }

            // ===============================
            // Cập nhật hành trình & hoạt động
            // ===============================
            _dbContext.TourItineraries.RemoveRange(original.TourItineraries);

            foreach (var draftItinerary in draft.TourItineraries.OrderBy(i => i.DayNumber))
            {
                var newItinerary = new TourItinerary
                {
                    DayNumber = draftItinerary.DayNumber,
                    ItineraryName = draftItinerary.ItineraryName,
                    Description = draftItinerary.Description,
                    CreatedBy = adminId,
                    CreatedDate = TimeHelper.GetVietnamTime(),
                    TourAttractions = new List<TourAttraction>()
                };

                foreach (var draftAttraction in draftItinerary.TourAttractions)
                {
                    var newAttraction = new TourAttraction
                    {
                        StartTime = draftAttraction.StartTime,
                        EndTime = draftAttraction.EndTime,
                        Description = draftAttraction.Description,
                        Localtion = draftAttraction.Localtion,
                        Price = draftAttraction.Price,
                        TourAttractionsName = draftAttraction.TourAttractionsName,
                        Category = draftAttraction.Category,
                        MapUrl = draftAttraction.MapUrl,
                        CreatedBy = adminId,
                        CreatedDate = TimeHelper.GetVietnamTime(),
                        TourAttractionImages = new List<TourAttractionImage>()
                    };

                    if (!string.IsNullOrWhiteSpace(draftAttraction.ImageUrl))
                    {
                        var image = new Image { ImageUrl = draftAttraction.ImageUrl };
                        _dbContext.Images.Add(image);

                        newAttraction.TourAttractionImages.Add(new TourAttractionImage
                        {
                            Image = image
                        });
                    }

                    newItinerary.TourAttractions.Add(newAttraction);
                }

                original.TourItineraries.Add(newItinerary);
            }

            // ===============================
            // Xoá bản nháp đúng cách
            // ===============================
            foreach (var tourImage in draft.TourImages)
            {
                if (tourImage.Image != null)
                {
                    var publicId = _imageUploadService.GetPublicIdFromUrl(tourImage.Image.ImageUrl);
                    await _imageUploadService.DeleteImageAsync(publicId);

                    tourImage.Image.RemovedDate = TimeHelper.GetVietnamTime();
                    tourImage.Image.RemovedBy = adminId;

                    _dbContext.Images.Remove(tourImage.Image);
                }

                _dbContext.TourImages.Remove(tourImage);
            }

            foreach (var itinerary in draft.TourItineraries)
            {
                foreach (var attraction in itinerary.TourAttractions)
                {
                    foreach (var image in attraction.TourAttractionImages)
                    {
                        if (image.Image != null)
                        {
                            var publicId = _imageUploadService.GetPublicIdFromUrl(image.Image.ImageUrl);
                            await _imageUploadService.DeleteImageAsync(publicId);

                            image.Image.RemovedDate = TimeHelper.GetVietnamTime();
                            image.Image.RemovedBy = adminId;

                            _dbContext.Images.Remove(image.Image);
                        }

                        _dbContext.TourAttractionImages.Remove(image);
                    }

                    _dbContext.TourAttractions.Remove(attraction);
                }

                _dbContext.TourItineraries.Remove(itinerary);
            }

            _dbContext.Tours.Remove(draft);

            await _dbContext.SaveChangesAsync();
        }
        public async Task<bool> RejectDraftAsync(int tourId, string reason, int adminId)
        {
            var draftTour = await _dbContext.Tours
                .Where(t => t.OriginalTourId == tourId && t.Status == TourStatuses.PendingApproval)
                .FirstOrDefaultAsync();

            if (draftTour == null)
                return false;

            draftTour.Status = TourStatuses.Rejected;
            draftTour.RejectReason = reason;
            draftTour.ModifiedDate = TimeHelper.GetVietnamTime();
            draftTour.ModifiedBy = adminId;

            await _dbContext.SaveChangesAsync();
            return true;
        }

    }
}
