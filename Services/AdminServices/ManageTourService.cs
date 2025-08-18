using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;
using static TripWiseAPI.Models.DTO.UpdateTourDto;
using static TripWiseAPI.Services.VnPayService;

namespace TripWiseAPI.Services.AdminServices
{
    public class ManageTourService : IManageTourService
    {
        private readonly TripWiseDBContext _dbContext;
        private readonly IImageUploadService _imageUploadService;
        private readonly FirebaseLogService _logService;
		public ManageTourService(TripWiseDBContext dbContext, IImageUploadService imageUploadService, FirebaseLogService firebaseLog)
        {
            _dbContext = dbContext;
            _imageUploadService = imageUploadService;
			_logService = firebaseLog;
		}
        public async Task<List<PendingTourDto>> GetToursByStatusAsync( string? status, int? partnerId, DateTime? fromDate, DateTime? toDate)
        {
            var query =
                from t in _dbContext.Tours
                    .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                    .Include(t => t.Partner)
                where t.RemovedDate == null
                      && t.TourTypesId == 2
                      && t.Status != TourStatuses.Draft
                join ot in _dbContext.Tours on t.OriginalTourId equals ot.TourId into g
                from originalTour in g.DefaultIfEmpty() // left join
                select new { Tour = t, OriginalTour = originalTour };

            // Nếu có truyền status cụ thể thì lọc theo status
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(x => x.Tour.Status == status);
            }

            // Nếu có filter partnerId
            if (partnerId.HasValue)
            {
                query = query.Where(x => x.Tour.PartnerId == partnerId.Value);
            }

            // Lọc theo ngày tạo
            if (fromDate.HasValue)
            {
                query = query.Where(x => x.Tour.CreatedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x => x.Tour.CreatedDate <= toDate.Value.Date.AddDays(1).AddSeconds(-1));
            }

            return await query
                .Select(x => new PendingTourDto
                {
                    TourId = x.Tour.TourId,
                    StartTime = x.Tour.StartTime,
                    TourName = x.Tour.TourName,
                    Description = x.Tour.Description,
                    Location = x.Tour.Location,
                    Price = (decimal)x.Tour.PriceAdult,
                    Status = x.Tour.Status,
                    PartnerID = x.Tour.PartnerId,
                    CompanyName = x.Tour.Partner.CompanyName,
                    CreatedDate = x.Tour.CreatedDate,
                    ModifiedDate = x.Tour.ModifiedDate,
                    ImageUrls = x.Tour.TourImages.Select(ti => ti.Image.ImageUrl).ToList(),
                    OriginalTourId = x.Tour.Status == TourStatuses.PendingApproval ? x.Tour.OriginalTourId : null,
                    IsUpdatedFromApprovedTour = x.Tour.Status == TourStatuses.PendingApproval && x.Tour.OriginalTourId != null,
                    UpdateNote = x.Tour.Status == TourStatuses.PendingApproval && x.OriginalTour != null
                        ? $"Bản cập nhật của tour {x.OriginalTour.TourName}"
                        : null
                })
                .ToListAsync();
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
			await _logService.LogAsync(userId: adminId, action: "Update", message: $"Duyệt tour {tour.TourId} - {tour.TourName}", statusCode: 200, modifiedBy: adminId, modifiedDate: tour.ModifiedDate);
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
			await _logService.LogAsync(userId: adminId, action: "Update", message: $"Chuyển tour {tour.TourId} - {tour.TourName} sang trạng thái chờ duyệt", statusCode: 200, modifiedBy: adminId, modifiedDate: tour.ModifiedDate);
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
			await _logService.LogAsync(userId: adminId, action: "Update", message: $"Admin đã từ chối tour {tour.TourId} - {tour.TourName} với lý do: {reason}", statusCode: 200, modifiedBy: adminId, modifiedDate: tour.ModifiedDate);
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
            // Mặc định null
            decimal? availableSlots = null;

            // 🔹 Chỉ tính nếu tour Approved
            if (tour.Status == "Approved")
            {
                var bookedCount = await _dbContext.Bookings
                    .Where(b => b.TourId == tour.TourId && b.BookingStatus == PaymentStatus.Success)
                    .SumAsync(b => (int?)b.Quantity) ?? 0;

                availableSlots = Math.Max(0, (decimal)(tour.MaxGroupSize - bookedCount));
            }

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
                            Category = a.Category,
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
                MaxGroupSize = (int)tour.MaxGroupSize,
                AvailableSlots = (int?)(availableSlots ?? 0),
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

                    // ===============================
                    // Đồng bộ ảnh cho Activity
                    // ===============================
                    foreach (var draftAttractionImage in draftAttraction.TourAttractionImages)
                    {
                        var image = new Image { ImageUrl = draftAttractionImage.Image.ImageUrl };
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
			await _logService.LogAsync(userId: adminId, action: "Update", message: $"Gửi bản nháp cập nhật tour {tourId}", statusCode: 200, modifiedBy: adminId, modifiedDate: TimeHelper.GetVietnamTime());
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
			await _logService.LogAsync(userId: adminId, action: "Update", message: $"Admin đã từ chối bản nháp của tour {tourId} với lý do: {reason}", statusCode: 200, modifiedBy: adminId, modifiedDate: draftTour.ModifiedDate);
			await _dbContext.SaveChangesAsync();
            return true;
        }

    }
}
