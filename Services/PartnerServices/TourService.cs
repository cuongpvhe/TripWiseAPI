using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static TripWiseAPI.Models.DTO.UpdateTourDto;
using static TripWiseAPI.Services.VnPayService;

namespace TripWiseAPI.Services.PartnerServices
{
    public class TourService : ITourService
    {
        private readonly TripWiseDBContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly IImageUploadService _imageUploadService;
        private readonly IConfiguration _configuration;
        private readonly FirebaseLogService _logService;

		public TourService(TripWiseDBContext dbContext, IWebHostEnvironment env, IImageUploadService imageUploadService, IConfiguration configuration, FirebaseLogService firebaseLogService)
        {
            _dbContext = dbContext;
            _env = env;
            _imageUploadService = imageUploadService;
            _configuration = configuration;
			_logService = firebaseLogService;
		}
        public async Task<List<PendingTourDto>> GetToursByStatusAsync(int partnerId, string? status, DateTime? fromDate, DateTime? toDate)

        {
            var query = _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image) 
                .Where(t => t.RemovedDate == null && t.TourTypesId == 2 && t.PartnerId == partnerId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }
            if (fromDate.HasValue)
            {
                query = query.Where(t => t.CreatedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                // Include full day by setting time to 23:59:59
                query = query.Where(t => t.CreatedDate <= toDate.Value.Date.AddDays(1).AddSeconds(-1));
            }
            var tours = await query
                .OrderByDescending(t => t.CreatedDate)
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
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList(),

                    // Nếu status là PendingApproval thì thêm các thông tin dưới
                    OriginalTourId = t.Status == TourStatuses.PendingApproval ? t.OriginalTourId : null,
                    IsUpdatedFromApprovedTour = t.Status == TourStatuses.PendingApproval && t.OriginalTourId != null,
                    UpdateNote = t.Status == TourStatuses.PendingApproval && t.OriginalTourId != null
                        ? $"Tour này là bản cập nhật của tour đã được duyệt trước đó TourId: ({t.OriginalTourId})"
                        : null
                })
                .ToListAsync();

            return tours;
        }

        public async Task<int> CreateTourAsync(CreateTourDto request, int partnerId)
        {
            
            if (string.IsNullOrWhiteSpace(request.TourName))
                throw new ArgumentException("Tên tour không được để trống");
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Mô tả tour không được để trống");
            if (!int.TryParse(request.Duration, out int duration) || duration <= 0)
                throw new ArgumentException("Thời lượng tour phải là số nguyên dương");
            if (string.IsNullOrWhiteSpace(request.Location))
                throw new ArgumentException("Địa điểm không được để trống");
            if (request.MaxGroupSize <= 0)
                throw new ArgumentException("Số lượng nhóm tối đa phải lớn hơn 0");
            if (string.IsNullOrWhiteSpace(request.Category))
                throw new ArgumentException("Danh mục không được để trống");
            if (string.IsNullOrWhiteSpace(request.TourNote))
                throw new ArgumentException("Ghi chú tour không được để trống");
            if (string.IsNullOrWhiteSpace(request.TourInfo))
                throw new ArgumentException("Thông tin tour không được để trống");
            if (!request.StartTime.HasValue)
                throw new ArgumentException("Thời gian bắt đầu không được để trống");
            var tour = new Tour
            {
                StartTime = request.StartTime,
                TourName = request.TourName,
                Description = request.Description,
                Duration = request.Duration,
                Location = request.Location,
                MaxGroupSize = request.MaxGroupSize,
                Category = request.Category,
                TourNote = request.TourNote,
                TourInfo = request.TourInfo,
                PriceAdult = request.PriceAdult,
                PriceChild5To10 = request.PriceChild5To10,
                PriceChildUnder5 = request.PriceChildUnder5,
                TourTypesId = 2,
                PartnerId = partnerId,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = partnerId
            };
			await _logService.LogAsync(partnerId, "Create", $"Tour '{request.TourName}' created successfully", 200, createdDate: TimeHelper.GetVietnamTime(), createdBy: partnerId);
			_dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();

            // Tải và gán nhiều ảnh từ file
            if (request.ImageFile != null && request.ImageFile.Any())
            {
                foreach (var file in request.ImageFile)
                {
                    var imageUrl = await _imageUploadService.UploadImageFromFileAsync(file);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        await AddTourImageAsync(tour.TourId, imageUrl, partnerId);
                    }
                }
            }

            // Tải và gán nhiều ảnh từ URL
            if (request.Image != null && request.Image.Any())
            {
                foreach (var url in request.Image)
                {
                    var imageUrl = await _imageUploadService.UploadImageFromUrlAsync(url);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        await AddTourImageAsync(tour.TourId, imageUrl, partnerId);
                    }
                }
            }

            return tour.TourId;
        }
        private async Task AddTourImageAsync(int tourId, string imageUrl, int partnerId)
        {
            var image = new Image
            {
                ImageUrl = imageUrl,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = partnerId
            };

            _dbContext.Images.Add(image);
            await _dbContext.SaveChangesAsync();

            var tourImage = new TourImage
            {
                TourId = tourId,
                ImageId = image.ImageId,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = partnerId
            };

            _dbContext.TourImages.Add(tourImage);
            await _dbContext.SaveChangesAsync();
        }


        public async Task<int> CreateItineraryAsync(int tourId, CreateItineraryDto request, int userId)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "Dữ liệu hành trình không được để trống.");

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Tiêu đề hành trình không được để trống.");
            if (request.DayNumber != null && request.DayNumber <= 0)
                throw new ArgumentException("Ngày trong hành trình phải lớn hơn 0.");
            var tourExists = await _dbContext.Tours.AnyAsync(t => t.TourId == tourId);
            if (!tourExists)
                throw new ArgumentException("Tour không tồn tại.");
            // Tạo đối tượng
            var itinerary = new TourItinerary
            {
                TourId = tourId,
                DayNumber = request.DayNumber ?? 1,
                ItineraryName = request.Title,
                Description = request.Description,
                CreatedBy = userId,
                CreatedDate = TimeHelper.GetVietnamTime()
            };

            _dbContext.TourItineraries.Add(itinerary);
            await _dbContext.SaveChangesAsync();
            return itinerary.ItineraryId;
        }

        public async Task<int> CreateActivityAsync(int itineraryId, ActivityDayDto request, int userId)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "Dữ liệu hoạt động không được để trống.");

            if (string.IsNullOrWhiteSpace(request.PlaceDetail))
                throw new ArgumentException("Tên địa điểm không được để trống.");

            if (request.EstimatedCost < 0)
                throw new ArgumentException("Chi phí ước tính phải lớn hơn 0.");

            // Lấy itinerary và tour tương ứng
            var itinerary = await _dbContext.TourItineraries
                .Include(i => i.Tour)
                .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId);

            var tourStartDate = itinerary.Tour.StartTime;

            if (tourStartDate == null)
                throw new ArgumentException("Tour chưa có thời gian bắt đầu.");

            if (!string.IsNullOrEmpty(request.StartTime) && !string.IsNullOrEmpty(request.EndTime))
            {
                if (!TimeSpan.TryParse(request.StartTime, out var startTime))
                    throw new ArgumentException("Thời gian bắt đầu không hợp lệ.");

                if (!TimeSpan.TryParse(request.EndTime, out var endTime))
                    throw new ArgumentException("Thời gian kết thúc không hợp lệ.");

                if (endTime <= startTime)
                    throw new ArgumentException("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");

                var tourDateOnly = tourStartDate.Value.Date;
                var today = TimeHelper.GetVietnamTime().Date;

                if (tourDateOnly == today)
                {
                    var activityStartDateTime = tourDateOnly.Add(startTime); // gộp ngày + giờ
                    var now = TimeHelper.GetVietnamTime();

                    if (activityStartDateTime <= now)
                        throw new ArgumentException("Thời gian bắt đầu phải lớn hơn thời gian hiện tại.");
                }

                // Nếu tourDate > hôm nay thì KHÔNG cần check với now
            }

            var attraction = new TourAttraction
            {
                TourAttractionsName = request.PlaceDetail,
                Description = request.Description,
                Price = request.EstimatedCost,
                Localtion = request.Address,
                Category = request.Category,
                StartTime = string.IsNullOrEmpty(request.StartTime) ? null : TimeSpan.Parse(request.StartTime),
                EndTime = string.IsNullOrEmpty(request.EndTime) ? null : TimeSpan.Parse(request.EndTime),
                MapUrl = request.MapUrl,
                CreatedBy = userId,
                CreatedDate = TimeHelper.GetVietnamTime(),
                ItineraryId = itineraryId
            };

            _dbContext.TourAttractions.Add(attraction);
            await _dbContext.SaveChangesAsync();

            // Chỉ dùng 1 ảnh - Ưu tiên ảnh file nếu có
            string? imageUrl = null;

            if (request.ImageFile != null)
            {
                imageUrl = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
            }
            else if (!string.IsNullOrEmpty(request.Image))
            {
                imageUrl = await _imageUploadService.UploadImageFromUrlAsync(request.Image);
            }

            if (!string.IsNullOrEmpty(imageUrl))
            {
                await AddTourAttractionImageAsync(attraction.TourAttractionsId, imageUrl, userId);
            }


            return attraction.TourAttractionsId;
        }
        private async Task AddTourAttractionImageAsync(int attractionId, string imageUrl, int userId)
        {
            var image = new Image
            {
                ImageUrl = imageUrl,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = userId
            };

            _dbContext.Images.Add(image);
            await _dbContext.SaveChangesAsync();

            var tourAttractionImage = new TourAttractionImage
            {
                TourAttractionId = attractionId,
                ImageId = image.ImageId,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = userId
            };

            _dbContext.TourAttractionImages.Add(tourAttractionImage);
            await _dbContext.SaveChangesAsync();
        }


        public async Task<bool> SubmitTourAsync(int tourId, int partnerId)
        {
            var tour = await _dbContext.Tours.FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == partnerId);
            if (tour == null) return false;

            tour.Status = TourStatuses.PendingApproval;
            tour.RejectReason = null;
            tour.ModifiedDate = TimeHelper.GetVietnamTime();
            tour.ModifiedBy = partnerId;
			await _logService.LogAsync(partnerId, "SubmitTour", $"Tour ID {tourId} submitted for {TourStatuses.PendingApproval}", 200, modifiedDate: TimeHelper.GetVietnamTime(), modifiedBy: partnerId);
			await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<TourDetailDto?> GetTourDetailAsync(int tourId, int userId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                .Include(t => t.TourImages)
                    .ThenInclude(ti => ti.Image)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == userId && t.RemovedDate == null);

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
                MaxGroupSize = (int)tour.MaxGroupSize,
                TotalEstimatedCost = tour.PriceAdult,
                TourInfo = tour.TourInfo,
                TourNote = tour.TourNote,
                Itinerary = itineraryDtos,
                Status = tour.Status,
                RejectReason = tour.RejectReason,
                ImageUrls = imageUrls,
                ImageIds = imageIds,
                OriginalTourId = tour.OriginalTourId,
                PriceAdult = (decimal)tour.PriceAdult,
                PriceChild5To10 = (decimal)tour.PriceChild5To10,
                PriceChildUnder5 = (decimal)tour.PriceChildUnder5,
                AvailableSlots = (int?)(availableSlots ?? 0)

            };

            return dto;
        }

        public async Task<bool> UpdateTourAsync(int tourId, UpdateTourDto request, int userId, List<IFormFile>? imageFiles, List<string>? imageUrls)
        {
            if (string.IsNullOrWhiteSpace(request.TourName))
                throw new ArgumentException("Tên tour không được để trống.");

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Mô tả không được để trống.");

            if (string.IsNullOrWhiteSpace(request.Location))
                throw new ArgumentException("Địa điểm không được để trống.");

            if (string.IsNullOrWhiteSpace(request.Duration) || !int.TryParse(request.Duration, out int durationInt))
                throw new ArgumentException("Thời lượng không hợp lệ. Vui lòng nhập số nguyên dương.");

            if (durationInt <= 0)
                throw new ArgumentException("Thời lượng phải lớn hơn 0.");

            var tour = await _dbContext.Tours
                .Include(t => t.TourImages)
                .ThenInclude(ti => ti.Image)
                .FirstOrDefaultAsync(t => t.TourId == tourId);

            if (tour == null) return false;
            tour.StartTime = request.StartTime;
            tour.TourName = request.TourName;
            tour.Description = request.Description;
            tour.Location = request.Location;
            tour.Duration = request.Duration;
            tour.Category = request.Category;
            tour.MaxGroupSize = request.MaxGroupSize;
            tour.PriceAdult = request.PriceAdult;
            tour.PriceChild5To10 = request.PriceChild5To10;
            tour.PriceChildUnder5 = request.PriceChildUnder5;
            tour.ModifiedBy = userId;
            tour.ModifiedDate = TimeHelper.GetVietnamTime();

            if (imageFiles != null)
            {
                foreach (var file in imageFiles)
                {
                    var url = await _imageUploadService.UploadImageFromFileAsync(file);
                    var image = new Image { ImageUrl = url };
                    _dbContext.Images.Add(image);
                    tour.TourImages.Add(new TourImage { Image = image });
                }
            }

            if (imageUrls != null)
            {
                foreach (var url in imageUrls)
                {
                    var uploadedUrl = await _imageUploadService.UploadImageFromUrlAsync(url);
                    var image = new Image { ImageUrl = uploadedUrl };
                    _dbContext.Images.Add(image);
                    tour.TourImages.Add(new TourImage { Image = image });
                }
            }
			await _logService.LogAsync(userId, "Update", $"Tour ID {tourId} updated successfully", 200, modifiedDate: TimeHelper.GetVietnamTime(), modifiedBy: userId);
			await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMultipleTourImagesAsync(List<int> imageIds, int userId)
        {
            var tourImages = await _dbContext.TourImages
                .Include(ti => ti.Image)
                .Where(ti => imageIds.Contains(ti.Image.ImageId) && ti.Tour.PartnerId == userId)
                .ToListAsync();

            if (tourImages == null || !tourImages.Any())
            {
                Console.WriteLine("Không tìm thấy ảnh nào để xoá.");
                return false;
            }

            foreach (var tourImage in tourImages)
            {
                tourImage.RemovedDate = TimeHelper.GetVietnamTime();
                tourImage.RemovedBy = userId;

                var publicId = _imageUploadService.GetPublicIdFromUrl(tourImage.Image.ImageUrl);
                await _imageUploadService.DeleteImageAsync(publicId);

                _dbContext.Images.Remove(tourImage.Image);
                _dbContext.TourImages.Remove(tourImage);
            }
			await _logService.LogAsync(userId, "DeleteTourImages", $"Deleted images: {string.Join(",", imageIds)}", 200, removedDate: TimeHelper.GetVietnamTime(), removedBy: userId);
			await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMultipleTourAttractionImagesAsync(List<int> imageIds, int userId)
        {
            if (imageIds == null || imageIds.Count == 0) return false;

            var attractionImages = await _dbContext.TourAttractionImages
                .Include(ai => ai.Image)
                .Where(ai => imageIds.Contains(ai.Image.ImageId))
                .ToListAsync();

            if (attractionImages.Count == 0) return false;

            foreach (var attractionImage in attractionImages)
            {
                attractionImage.RemovedDate = TimeHelper.GetVietnamTime();
                attractionImage.RemovedBy = userId;

                var publicId = _imageUploadService.GetPublicIdFromUrl(attractionImage.Image.ImageUrl);
                await _imageUploadService.DeleteImageAsync(publicId);

                _dbContext.Images.Remove(attractionImage.Image);
                _dbContext.TourAttractionImages.Remove(attractionImage);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }


        public async Task<bool> UpdateItineraryAsync(int itineraryId, int userId, CreateItineraryDto request)
        {
            var itinerary = await _dbContext.TourItineraries.FindAsync(itineraryId);
            if (itinerary == null) return false;

            itinerary.ItineraryName = request.Title;
            itinerary.DayNumber = request.DayNumber;
            itinerary.ModifiedBy = userId;
            itinerary.ModifiedDate = TimeHelper.GetVietnamTime();
			await _logService.LogAsync(userId, "Update", $"Updated itinerary ID {itineraryId}", 200, modifiedDate: TimeHelper.GetVietnamTime(), modifiedBy: userId);
			await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateActivityAsync(int activityId, int userId, UpdateActivityDto request)
        {
            var activity = await _dbContext.TourAttractions
                .Include(a => a.TourAttractionImages)
                    .ThenInclude(ai => ai.Image)
                .FirstOrDefaultAsync(a => a.TourAttractionsId == activityId);

            if (activity == null) return false;

            activity.StartTime = string.IsNullOrEmpty(request.StartTime) ? null : TimeSpan.Parse(request.StartTime);
            activity.EndTime = string.IsNullOrEmpty(request.EndTime) ? null : TimeSpan.Parse(request.EndTime);
            activity.Description = request.Description;
            activity.Localtion = request.Address;
            activity.TourAttractionsName = request.PlaceDetail;
            activity.Category = request.Category;
            activity.MapUrl = request.MapUrl;
            activity.ModifiedBy = userId;
            activity.ModifiedDate = TimeHelper.GetVietnamTime();

            bool hasNewImage = request.ImageFile != null || !string.IsNullOrEmpty(request.Image);

            if (hasNewImage)
            {
                // Xoá ảnh cũ
                if (activity.TourAttractionImages.Any())
                {
                    foreach (var ai in activity.TourAttractionImages.ToList())
                    {
                        if (ai.Image != null)
                        {
                            var publicId = _imageUploadService.GetPublicIdFromUrl(ai.Image.ImageUrl);
                            await _imageUploadService.DeleteImageAsync(publicId);

                            ai.Image.RemovedDate = TimeHelper.GetVietnamTime();
                            ai.Image.RemovedBy = userId;
                            _dbContext.Images.Remove(ai.Image);
                        }
                        _dbContext.TourAttractionImages.Remove(ai);
                    }
                    activity.TourAttractionImages.Clear();
                }

                // Thêm ảnh mới từ file
                if (request.ImageFile != null)
                {
                    var url = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
                    var image = new Image { ImageUrl = url };
                    _dbContext.Images.Add(image);
                    activity.TourAttractionImages.Add(new TourAttractionImage { Image = image });
                }
                // Thêm ảnh mới từ URL
                else if (!string.IsNullOrEmpty(request.Image))
                {
                    var uploadedUrl = await _imageUploadService.UploadImageFromUrlAsync(request.Image);
                    var image = new Image { ImageUrl = uploadedUrl };
                    _dbContext.Images.Add(image);
                    activity.TourAttractionImages.Add(new TourAttractionImage { Image = image });
                }
            }

            await _logService.LogAsync(userId, "Update", $"Updated activity ID {activityId}", 200, modifiedDate: TimeHelper.GetVietnamTime(), modifiedBy: userId);
            await _dbContext.SaveChangesAsync();
            return true;
        }


        public async Task<bool> DeleteItineraryAsync(int userId, int itineraryId)
        {
            var itinerary = await _dbContext.TourItineraries
                .Include(i => i.TourAttractions)
                    .ThenInclude(a => a.TourAttractionImages)
                    .ThenInclude(img => img.Image)
                .FirstOrDefaultAsync(i => i.ItineraryId == itineraryId);

            if (itinerary == null) return false;

            foreach (var activity in itinerary.TourAttractions)
            {
                activity.RemovedDate = TimeHelper.GetVietnamTime();
                activity.RemovedBy = userId;

                // Xóa ảnh của Activity
                foreach (var image in activity.TourAttractionImages)
                {
                    if (image.Image != null)
                    {
                        // ✅ Xóa ảnh khỏi cloud
                        var publicId = _imageUploadService.GetPublicIdFromUrl(image.Image.ImageUrl);
                        await _imageUploadService.DeleteImageAsync(publicId); // Giả sử bạn lưu PublicId

                        image.Image.RemovedDate = TimeHelper.GetVietnamTime();
                        image.Image.RemovedBy = userId;
                        _dbContext.Images.Remove(image.Image);
                    }
                    _dbContext.TourAttractionImages.Remove(image);
                }

                _dbContext.TourAttractions.Remove(activity);
            }

            itinerary.RemovedDate = TimeHelper.GetVietnamTime();
            itinerary.RemovedBy = userId;
            _dbContext.TourItineraries.Remove(itinerary);
			await _logService.LogAsync(userId, "Delete", $"Deleted itinerary ID {itineraryId}", 200, removedDate: TimeHelper.GetVietnamTime(), removedBy: userId);
			await _dbContext.SaveChangesAsync();
            return true;
        }


        public async Task<bool> DeleteActivityAsync(int userId, int activityId)
        {
            var activity = await _dbContext.TourAttractions
                .Include(a => a.TourAttractionImages)
                .ThenInclude(ai => ai.Image)
                .FirstOrDefaultAsync(a => a.TourAttractionsId == activityId);

            if (activity == null) return false;

            activity.RemovedDate = TimeHelper.GetVietnamTime();
            activity.RemovedBy = userId;

            foreach (var attractionImage in activity.TourAttractionImages)
            {
                var image = attractionImage.Image;
                if (image != null)
                {
                    // ✅ Xóa ảnh khỏi cloud
                    var publicId = _imageUploadService.GetPublicIdFromUrl(attractionImage.Image.ImageUrl);
                    await _imageUploadService.DeleteImageAsync(publicId); 

                    // ✅ Xóa khỏi DB
                    image.RemovedDate = TimeHelper.GetVietnamTime();
                    image.RemovedBy = userId;
                    _dbContext.Images.Remove(image);
                }

                _dbContext.TourAttractionImages.Remove(attractionImage);
            }
			await _logService.LogAsync(userId, "Delete", $"Deleted activity ID {activityId}", 200, removedDate: TimeHelper.GetVietnamTime(), removedBy: userId);
			_dbContext.TourAttractions.Remove(activity);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteOrDraftTourAsync(int tourId, string action, int partnerId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourImages)
                    .ThenInclude(ti => ti.Image)
                .Include(t => t.TourItineraries)
                    .ThenInclude(it => it.TourAttractions)
                        .ThenInclude(a => a.TourAttractionImages)
                            .ThenInclude(tai => tai.Image)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == partnerId);

            if (tour == null) return false;

            if (action == "delete")
            {
                // Xoá ảnh của tour
                foreach (var tourImage in tour.TourImages)
                {
                    if (tourImage.Image != null)
                    {
                        var publicId = _imageUploadService.GetPublicIdFromUrl(tourImage.Image.ImageUrl);
                        await _imageUploadService.DeleteImageAsync(publicId);

                        tourImage.Image.RemovedDate = TimeHelper.GetVietnamTime();
                        tourImage.Image.RemovedBy = partnerId;

                        _dbContext.Images.Remove(tourImage.Image);
                    }

                    _dbContext.TourImages.Remove(tourImage);
                }

                // Xoá từng itinerary và toàn bộ ảnh hoạt động bên trong
                foreach (var itinerary in tour.TourItineraries)
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
                                image.Image.RemovedBy = partnerId;

                                _dbContext.Images.Remove(image.Image);
                            }

                            _dbContext.TourAttractionImages.Remove(image);
                        }

                        attraction.RemovedDate = TimeHelper.GetVietnamTime();
                        attraction.RemovedBy = partnerId;

                        _dbContext.TourAttractions.Remove(attraction);
                    }

                    itinerary.RemovedDate = TimeHelper.GetVietnamTime();
                    itinerary.RemovedBy = partnerId;

                    _dbContext.TourItineraries.Remove(itinerary);
                }

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
			string messageLog = action == "Delete" ? $"Tour ID {tourId} deleted by partner" : $"Tour ID {tourId} moved to Draft";
			await _logService.LogAsync(partnerId, "Delete", messageLog, 200, removedDate: TimeHelper.GetVietnamTime(), removedBy: partnerId);
			await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<List<Tour>> GetToursByLocationAsync(string location, int maxResults = 4)
        {
            return await _dbContext.Tours
                .Include(t => t.TourImages)
                    .ThenInclude(ti => ti.Image)
                .Where(t =>
                    t.Location != null &&
                    t.Location.ToLower().Contains(location.ToLower()) &&
                    t.TourTypesId == 2 && 
                    t.Status == "Approved" &&
                    t.RemovedDate == null)
                .OrderByDescending(t => t.CreatedDate)
                .Take(maxResults)
                .ToListAsync();
        }

        public async Task<TourDraftDto?> GetOrCreateDraftAsync(int tourId)
        {
            // Nếu đã có bản nháp thì trả về luôn
            var existingDraft = await _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Include(t => t.TourItineraries)
                    .ThenInclude(i => i.TourAttractions)
                        .ThenInclude(a => a.TourAttractionImages)
                            .ThenInclude(ai => ai.Image)
                .FirstOrDefaultAsync(t => t.OriginalTourId == tourId && t.Status == TourStatuses.Draft);

            Tour draftTour;

            if (existingDraft != null)
            {
                draftTour = existingDraft;
            }
            else
            {
                var originalTour = await _dbContext.Tours
                    .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                    .Include(t => t.TourItineraries)
                        .ThenInclude(i => i.TourAttractions)
                            .ThenInclude(a => a.TourAttractionImages)
                                .ThenInclude(ai => ai.Image)
                    .FirstOrDefaultAsync(t => t.TourId == tourId && t.Status == TourStatuses.Approved);

                if (originalTour == null)
                    return null;

                draftTour = new Tour
                {
                    StartTime = originalTour.StartTime,
                    TourName = originalTour.TourName,
                    Description = originalTour.Description,
                    Duration = originalTour.Duration,
                    Location = originalTour.Location,
                    Category = originalTour.Category,
                    MaxGroupSize = originalTour.MaxGroupSize,
                    TourNote = originalTour.TourNote,
                    TourInfo = originalTour.TourInfo,   
                    Status = TourStatuses.Draft,
                    PriceAdult = originalTour.PriceAdult,
                    PriceChild5To10 = originalTour.PriceChild5To10,
                    PriceChildUnder5 = originalTour.PriceChildUnder5,
                    CreatedBy = originalTour.CreatedBy,
                    CreatedDate = TimeHelper.GetVietnamTime(),
                    TourTypesId = originalTour.TourTypesId,
                    OriginalTourId = originalTour.TourId,
                    PartnerId = originalTour.PartnerId,
                    TourImages = new List<TourImage>(),
                    TourItineraries = new List<TourItinerary>()
                };

                foreach (var tourImage in originalTour.TourImages)
                {
                    var image = new Image { ImageUrl = tourImage.Image.ImageUrl };
                    _dbContext.Images.Add(image);
                    draftTour.TourImages.Add(new TourImage { Image = image });
                }

                foreach (var itinerary in originalTour.TourItineraries.OrderBy(i => i.DayNumber))
                {
                    var draftItinerary = new TourItinerary
                    {
                        DayNumber = itinerary.DayNumber,
                        ItineraryName = itinerary.ItineraryName,
                        Description = itinerary.Description,
                        CreatedBy = itinerary.CreatedBy,
                        CreatedDate = TimeHelper.GetVietnamTime(),
                        TourAttractions = new List<TourAttraction>()
                    };

                    foreach (var activity in itinerary.TourAttractions)
                    {
                        var draftActivity = new TourAttraction
                        {
                            StartTime = activity.StartTime,
                            EndTime = activity.EndTime,
                            Description = activity.Description,
                            Localtion = activity.Localtion,
                            Price = activity.Price,
                            TourAttractionsName = activity.TourAttractionsName,
                            Category = activity.Category,
                            MapUrl = activity.MapUrl,
                            CreatedBy = activity.CreatedBy,
                            CreatedDate = TimeHelper.GetVietnamTime(),
                            TourAttractionImages = new List<TourAttractionImage>()
                        };

                        // Copy ảnh (chỉ 1 ảnh duy nhất cho mỗi activity)
                        var originalImageUrl = activity.TourAttractionImages.FirstOrDefault()?.Image?.ImageUrl;
                        if (!string.IsNullOrEmpty(originalImageUrl))
                        {
                            var image = new Image
                            {
                                ImageUrl = originalImageUrl,
                                CreatedDate = TimeHelper.GetVietnamTime(),
                                CreatedBy = activity.CreatedBy
                            };
                            _dbContext.Images.Add(image);

                            draftActivity.TourAttractionImages.Add(new TourAttractionImage
                            {
                                Image = image,
                                CreatedDate = TimeHelper.GetVietnamTime(),
                                CreatedBy = activity.CreatedBy
                            });
                        }

                        draftItinerary.TourAttractions.Add(draftActivity);
                    }


                    draftTour.TourItineraries.Add(draftItinerary);
                }

                _dbContext.Tours.Add(draftTour);
                await _dbContext.SaveChangesAsync();
            }

            // Map sang DTO để trả về
            var dto = new TourDraftDto
            {
                TourId = draftTour.TourId,
                TourName = draftTour.TourName,
                Description = draftTour.Description,
                Duration = draftTour.Duration,
                MaxGroupSize = draftTour.MaxGroupSize?.ToString(),
                Location = draftTour.Location,
                Category = draftTour.Category,
                TourInfo = draftTour.TourInfo,
                TourNote = draftTour.TourNote,
                StartTime = draftTour.StartTime,
                Status = draftTour.Status,
                PriceAdult= (decimal)draftTour.PriceAdult,
                PriceChild5To10 = (decimal)draftTour.PriceChild5To10,
                PriceChildUnder5 = (decimal)draftTour.PriceChildUnder5,
                PartnerID = draftTour.PartnerId,
                OriginalTourId = draftTour.OriginalTourId,
                TourTypesId = draftTour.TourTypesId,
                CreatedDate = draftTour.CreatedDate,
                CreatedBy = draftTour.CreatedBy,
                TourImages = draftTour.TourImages.Select(ti => ti.Image.ImageUrl).ToList(),
                TourItineraries = draftTour.TourItineraries.OrderBy(i => i.DayNumber).Select(i => new ItineraryDto
                {
                    DayNumber = i.DayNumber,
                    Title = i.ItineraryName,
                    Description = i.Description,
                    Activities = i.TourAttractions.Select(a => new ActivityDto
                    {
                        Description = a.Description,
                        StartTime = a.StartTime.HasValue ? a.StartTime.Value.ToString(@"hh\:mm") : null,
                        EndTime = a.EndTime.HasValue ? a.EndTime.Value.ToString(@"hh\:mm") : null,
                        Address = a.Localtion,
                        EstimatedCost = a.Price,
                        Category = a.Category,
                        MapUrl = a.MapUrl,
                        ImageUrls = a.TourAttractionImages.FirstOrDefault()?.Image.ImageUrl
                    }).ToList()
                }).ToList()
            };

            return dto;
        }

        public async Task SendDraftToAdminAsync(int tourId, int userId)
        {
            var draft = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.OriginalTourId == tourId && t.Status == TourStatuses.Draft && t.CreatedBy == userId);

            if (draft == null) throw new Exception("Draft not found");

            draft.Status = TourStatuses.PendingApproval;
            draft.ModifiedDate = TimeHelper.GetVietnamTime();
            draft.ModifiedBy = userId;
            await _dbContext.SaveChangesAsync();

            // TODO: Thêm gửi thông báo/email cho admin nếu cần
        }

        public async Task<bool> ResubmitRejectedDraftAsync(int originalTourId, int partnerId)
        {
            var rejectedDraft = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.OriginalTourId == originalTourId
                                          && t.Status == TourStatuses.Rejected
                                          && (t.CreatedBy == partnerId || t.PartnerId == partnerId));

            if (rejectedDraft == null)
            {
                // Log để debug
                var drafts = await _dbContext.Tours
                    .Where(t => t.OriginalTourId == originalTourId)
                    .ToListAsync();
                Console.WriteLine("All drafts for originalTourId: " + originalTourId);
                foreach (var d in drafts)
                {
                    Console.WriteLine($"Id={d.TourId}, Status={d.Status}, CreatedBy={d.CreatedBy}, PartnerId={d.PartnerId}");
                }

                return false;
            }

            rejectedDraft.Status = TourStatuses.PendingApproval;
            rejectedDraft.RejectReason = null;
            rejectedDraft.ModifiedDate = TimeHelper.GetVietnamTime();
            rejectedDraft.ModifiedBy = partnerId;

            await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<PartnerTourStatisticsDto> GetPartnerTourStatisticsAsync(int partnerId, DateTime? fromDate, DateTime? toDate)
        {
            PartnerTourStatisticsDto result = null;

            using var conn = new SqlConnection(_configuration.GetConnectionString("DBContext"));
            using var command = new SqlCommand("sp_GetPartnerTourStatistics", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@PartnerId", partnerId);
            command.Parameters.AddWithValue("@FromDate", (object?)fromDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@ToDate", (object?)toDate ?? DBNull.Value);

            await conn.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                result = new PartnerTourStatisticsDto
                {
                    PartnerID = reader.GetInt32(0),
                    CompanyName = reader.GetString(1),
                    TotalTours = reader.GetInt32(2),
                    TotalBookedTours = reader.GetInt32(3),
                    TotalRevenue = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
                };
            }

            return result ?? new PartnerTourStatisticsDto();
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
