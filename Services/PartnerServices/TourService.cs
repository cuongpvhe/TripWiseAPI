using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Diagnostics;
using System.Linq;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Services.PartnerServices
{
    public class TourService : ITourService
    {
        private readonly TripWiseDBContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly IImageUploadService _imageUploadService;
        public TourService(TripWiseDBContext dbContext, IWebHostEnvironment env, IImageUploadService imageUploadService)
        {
            _dbContext = dbContext;
            _env = env;
            _imageUploadService = imageUploadService;
        }
        public async Task<List<PendingTourDto>> GetToursByStatusAsync(int partnerId, string? status)
        {
            var query = _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image) 
                .Where(t => t.RemovedDate == null && t.TourTypesId == 2 && t.PartnerId == partnerId);

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

        public async Task<int> CreateTourAsync(CreateTourDto request, int partnerId)
        {
            
            if (string.IsNullOrWhiteSpace(request.TourName))
                throw new ArgumentException("Tên tour không được để trống");
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Mô tả tour không được để trống");
            if (!int.TryParse(request.Duration, out int duration) || duration <= 0)
                throw new ArgumentException("Thời lượng tour phải là số nguyên dương");
            if (request.Price <= 0)
                throw new ArgumentException("Giá tour phải lớn hơn 0");
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
                Price = request.Price,
                Location = request.Location,
                MaxGroupSize = request.MaxGroupSize,
                Category = request.Category,
                TourNote = request.TourNote,
                TourInfo = request.TourInfo,
                TourTypesId = 2,
                PartnerId = partnerId,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = partnerId
            };

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

            if (!string.IsNullOrEmpty(request.StartTime) && !string.IsNullOrEmpty(request.EndTime))
            {
                if (!TimeSpan.TryParse(request.StartTime, out var startTime))
                    throw new ArgumentException("Thời gian bắt đầu không hợp lệ.");

                if (!TimeSpan.TryParse(request.EndTime, out var endTime))
                    throw new ArgumentException("Thời gian kết thúc không hợp lệ.");

                if (endTime <= startTime)
                    throw new ArgumentException("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");

                var now = TimeHelper.GetVietnamTime().TimeOfDay;
                if (startTime <= now)
                    throw new ArgumentException("Thời gian bắt đầu phải lớn hơn thời gian hiện tại.");
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
                RejectReason = tour.RejectReason,
                ImageUrls = imageUrls,
                ImageIds = imageIds,
                OriginalTourId = tour.OriginalTourId
                
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

            if (request.Price < 0 )
                throw new ArgumentException("Giá phải lớn hơn hoặc bằng 0.");

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
            tour.Price = request.Price;
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
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateActivityAsync(int activityId, int userId, ActivityDto request, List<IFormFile>? imageFiles, List<string>? imageUrls)
        {
            var activity = await _dbContext.TourAttractions.FindAsync(activityId);
            if (activity == null) return false;

            activity.StartTime = string.IsNullOrEmpty(request.StartTime) ? null : TimeSpan.Parse(request.StartTime);
            activity.EndTime = string.IsNullOrEmpty(request.EndTime) ? null : TimeSpan.Parse(request.EndTime);
            activity.Description = request.Description;
            activity.Localtion = request.Address;
            activity.Price = request.EstimatedCost;
            activity.TourAttractionsName= request.PlaceDetail;
            activity.Category = request.Category;
            activity.MapUrl = request.MapUrl;
            activity.ModifiedBy = userId;
            activity.ModifiedDate = TimeHelper.GetVietnamTime();
            if (imageFiles != null)
            {
                foreach (var file in imageFiles)
                {
                    var url = await _imageUploadService.UploadImageFromFileAsync(file);
                    var image = new Image { ImageUrl = url };
                    _dbContext.Images.Add(image);
                    activity.TourAttractionImages.Add(new TourAttractionImage { Image = image });
                }
            }

            if (imageUrls != null)
            {
                foreach (var url in imageUrls)
                {
                    var uploadedUrl = await _imageUploadService.UploadImageFromUrlAsync(url);
                    var image = new Image { ImageUrl = uploadedUrl };
                    _dbContext.Images.Add(image);
                    activity.TourAttractionImages.Add(new TourAttractionImage { Image = image });
                }
            }
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
                    Price = originalTour.Price,
                    MaxGroupSize = originalTour.MaxGroupSize,
                    TourNote = originalTour.TourNote,
                    TourInfo = originalTour.TourInfo,   
                    Status = TourStatuses.Draft,
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
                            ImageUrl = activity.TourAttractionImages.FirstOrDefault()?.Image?.ImageUrl
                        };

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
                Price = (decimal)draftTour.Price,
                TourInfo = draftTour.TourInfo,
                TourNote = draftTour.TourNote,
                StartTime = draftTour.StartTime,
                Status = draftTour.Status,
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
                        ImageUrls = a.ImageUrl
                    }).ToList()
                }).ToList()
            };

            return dto;
        }


        public async Task CancelDraftAsync(int tourId, int userId)
        {
            var draft = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.OriginalTourId == tourId && t.Status == TourStatuses.Draft && t.CreatedBy == userId);

            if (draft != null)
            {
                _dbContext.Tours.Remove(draft);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task SendDraftToAdminAsync(int tourId, int userId)
        {
            var draft = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.OriginalTourId == tourId && t.Status == TourStatuses.Draft && t.CreatedBy == userId);

            if (draft == null) throw new Exception("Draft not found");

            draft.Status = TourStatuses.PendingApproval;
            await _dbContext.SaveChangesAsync();

            // TODO: Thêm gửi thông báo/email cho admin nếu cần
        }

        public async Task SubmitDraftAsync(int tourId, int userId)
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
            original.Price = draft.Price;
            original.MaxGroupSize = draft.MaxGroupSize;
            original.TourNote = draft.TourNote;
            original.TourInfo = draft.TourInfo;
            original.Category = draft.Category;
            original.Location = draft.Location;
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
                    CreatedBy = userId,
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
                        CreatedBy = userId,
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
                    tourImage.Image.RemovedBy = userId;

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
                            image.Image.RemovedBy = userId;

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


    }

    public static class TourStatuses
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

}
