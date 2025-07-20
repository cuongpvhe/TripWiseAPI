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
        public async Task<List<PendingTourDto>> GetToursByStatusAsync(string? status)
        {
            var query = _dbContext.Tours
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image) 
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
                    Price = (decimal)t.Price,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList()
                })
                .ToListAsync();
        }

        public async Task<int> CreateTourAsync(CreateTourDto request, int partnerId)
        {

            string? imageUrl = null;

            if (request.ImageFile != null)
            {
                imageUrl = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
            }
            else if (!string.IsNullOrEmpty(request.Image))
            {
                imageUrl = await _imageUploadService.UploadImageFromUrlAsync(request.Image);
            }

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
                PartnerId = partnerId,
                PricePerDay = request.PricePerDay,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = partnerId
            };

            _dbContext.Tours.Add(tour);
            await _dbContext.SaveChangesAsync();
            if (!string.IsNullOrEmpty(imageUrl))
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
                    TourId = tour.TourId,
                    ImageId = image.ImageId,
                    CreatedDate = TimeHelper.GetVietnamTime(),
                    CreatedBy = partnerId
                };

                _dbContext.TourImages.Add(tourImage);
                await _dbContext.SaveChangesAsync();
            }

            return tour.TourId;
        }

        public async Task<int> CreateItineraryAsync(int tourId, CreateItineraryDto request, int userId)
        {
            var itinerary = new TourItinerary
            {
                TourId = tourId,
                DayNumber = request.DayNumber ?? 1,
                ItineraryName = request.Title,
                CreatedBy = userId,
                CreatedDate = TimeHelper.GetVietnamTime()
            };

            _dbContext.TourItineraries.Add(itinerary);
            await _dbContext.SaveChangesAsync();
            return itinerary.ItineraryId;
        }

        public async Task<int> CreateActivityAsync(int itineraryId, ActivityDayDto request, int userId)
        {

            string? imageUrl = null;

            if (request.ImageFile != null)
            {
                imageUrl = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
            }
            else if (!string.IsNullOrEmpty(request.Image))
            {
                imageUrl = await _imageUploadService.UploadImageFromUrlAsync(request.Image);
            }

            var attraction = new TourAttraction
            {
                TourAttractionsName = request.PlaceDetail,
                Description = request.Description,
                Price = request.EstimatedCost,
                Localtion = request.Address,
                Category = request.Category,
                StartTime = string.IsNullOrEmpty(request.StartTime)? null: TimeSpan.Parse(request.StartTime),
                EndTime = string.IsNullOrEmpty(request.EndTime)? null: TimeSpan.Parse(request.EndTime),
                MapUrl = request.MapUrl,
                ImageUrl = imageUrl,
                CreatedBy = userId,
                CreatedDate = TimeHelper.GetVietnamTime(),
                ItineraryId = itineraryId
            };

            _dbContext.TourAttractions.Add(attraction);
            await _dbContext.SaveChangesAsync();
            if (!string.IsNullOrEmpty(imageUrl))
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
                    TourAttractionId = attraction.TourAttractionsId,
                    ImageId = image.ImageId,
                    CreatedDate = TimeHelper.GetVietnamTime(),
                    CreatedBy = userId
                };

                _dbContext.TourAttractionImages.Add(tourAttractionImage);
                await _dbContext.SaveChangesAsync();
            }
            return attraction.TourAttractionsId;
       
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
                    Activities = attractions.Select(a => new ActivityDetailDto
                    {
                        AttractionId = a.TourAttractionsId,
                        StartTime = a.StartTime ?? TimeSpan.Zero,
                        EndTime = a.EndTime ?? TimeSpan.Zero,
                        Description = a.TourAttractionsName,
                        Address = a.Localtion,
                        EstimatedCost = a.Price,
                        PlaceDetail = a.Description,
                        MapUrl = a.MapUrl,
                        ImageUrls = a.TourAttractionImages
                            .Where(ai => ai.Image != null && ai.Image.RemovedDate == null)
                            .Select(ai => ai.Image.ImageUrl)
                            .ToList(),
                        ImageIds = a.TourAttractionImages
                            .Where(ai => ai.Image != null && ai.Image.RemovedDate == null)
                            .Select(ai => ai.Image.ImageId.ToString())
                            .ToList()
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
                TourName = tour.TourName,
                Description = tour.Description,
                TravelDate = tour.CreatedDate,
                Days = tour.Duration,
                Location = tour.Location,
                Preferences = tour.Category,
                Budget = null,
                TotalEstimatedCost = tour.Price,
                PricePerDay = tour.PricePerDay,
                TourInfo = tour.TourInfo,
                TourNote = tour.TourNote,
                Itinerary = itineraryDtos,
                Status = tour.Status,
                RejectReason = tour.RejectReason,
                ImageUrls = imageUrls,
                ImageIds = imageIds
            };

            return dto;
        }

        public async Task<bool> UpdateTourAsync(int tourId, UpdateTourDto request, int userId, List<IFormFile>? imageFiles, List<string>? imageUrls)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourImages)
                .ThenInclude(ti => ti.Image)
                .FirstOrDefaultAsync(t => t.TourId == tourId);

            if (tour == null) return false;

            tour.TourName = request.TourName;
            tour.Description = request.Description;
            tour.Location = request.Location;
            tour.Duration = request.Duration;
            tour.Category = request.Category;
            tour.Price = request.Price;
            tour.PricePerDay = request.PricePerDay;
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

        public async Task<bool> DeleteTourImageAsync(int imageId, int userId)
        {
            var tourImage = await _dbContext.TourImages
                .Include(ti => ti.Image)
                .FirstOrDefaultAsync(ti => ti.TourImageId == imageId);
            if (tourImage == null) return false;
            tourImage.RemovedDate = TimeHelper.GetVietnamTime();
            tourImage.RemovedBy = userId;
            var publicId = _imageUploadService.GetPublicIdFromUrl(tourImage.Image.ImageUrl);
            await _imageUploadService.DeleteImageAsync(publicId);

            _dbContext.Images.Remove(tourImage.Image);
            _dbContext.TourImages.Remove(tourImage);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTourAttractionImageAsync(int imageId, int userId)
        {
            var attractionImage = await _dbContext.TourAttractionImages
                .Include(ai => ai.Image)
                .FirstOrDefaultAsync(ai => ai.TourAttractionImageId == imageId);
            if (attractionImage == null) return false;
            attractionImage.RemovedDate = TimeHelper.GetVietnamTime();
            attractionImage.RemovedBy = userId;
            var publicId = _imageUploadService.GetPublicIdFromUrl(attractionImage.Image.ImageUrl);
            await _imageUploadService.DeleteImageAsync(publicId);

            _dbContext.Images.Remove(attractionImage.Image);
            _dbContext.TourAttractionImages.Remove(attractionImage);
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

        public async Task<bool> UpdateActivityAsync(int activityId, int userId, ActivityDayDto request, List<IFormFile>? imageFiles, List<string>? imageUrls)
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

        //public async Task<bool> AddItineraryAsync(int tourId, int userId, CreateItineraryDto request)
        //{
        //    var tour = await _dbContext.Tours.FindAsync(tourId);
        //    if (tour == null) return false;

        //    var itinerary = new TourItinerary
        //    {
        //        TourId = tourId,
        //        DayNumber = request.DayNumber,
        //        ItineraryName = request.Title,
        //        CreatedDate = TimeHelper.GetVietnamTime(),
        //        CreatedBy = userId
        //    };
        //    _dbContext.TourItineraries.Add(itinerary);
        //    await _dbContext.SaveChangesAsync();
        //    return true;
        //}

        //public async Task<bool> AddActivityAsync(int itineraryId, int userId, ActivityDayDto request, List<IFormFile>? imageFiles, List<string>? imageUrls)
        //{
        //    var itinerary = await _dbContext.TourItineraries.FindAsync(itineraryId);
        //    if (itinerary == null) return false;

        //    var activity = new TourAttraction
        //    {
        //        ItineraryId = itineraryId,
        //        StartTime = string.IsNullOrEmpty(request.StartTime) ? null : TimeSpan.Parse(request.StartTime),
        //        EndTime = string.IsNullOrEmpty(request.EndTime) ? null : TimeSpan.Parse(request.EndTime),
        //        Description = request.Description,
        //        Localtion = request.Address,
        //        Price = request.EstimatedCost,
        //        TourAttractionsName = request.PlaceDetail,
        //        Category = request.Category,
        //        MapUrl = request.MapUrl,
        //        CreatedDate = TimeHelper.GetVietnamTime(),
        //        CreatedBy = userId
        //    };
        //    _dbContext.TourAttractions.Add(activity);
        //    await _dbContext.SaveChangesAsync();
        //    return true;
        //}

        public async Task<bool> DeleteItineraryAsync(int userId, int itineraryId)
        {
            var itinerary = await _dbContext.TourItineraries.FindAsync(itineraryId);
            if (itinerary == null) return false;
            itinerary.RemovedDate = TimeHelper.GetVietnamTime();
            itinerary.RemovedBy = userId;
            _dbContext.TourItineraries.Remove(itinerary);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteActivityAsync(int userId, int activityId)
        {
            var activity = await _dbContext.TourAttractions.FindAsync(activityId);
            if (activity == null) return false;
            activity.RemovedDate = TimeHelper.GetVietnamTime();
            activity.RemovedBy = userId;
            _dbContext.TourAttractions.Remove(activity);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteOrDraftTourAsync(int tourId, string action, int partnerId)
        {
            var tour = await _dbContext.Tours
                .Include(t => t.TourItineraries)
                    .ThenInclude(it => it.TourAttractions) // include các attraction của mỗi itinerary
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.CreatedBy == partnerId);

            if (tour == null) return false;

            if (action == "delete")
            {
                // Lấy tất cả các attraction liên kết với itinerary
                var attractionsToDelete = tour.TourItineraries
                    .SelectMany(it => it.TourAttractions)
                    .ToList();

                // Xoá attractions
                _dbContext.TourAttractions.RemoveRange(attractionsToDelete);

                // Xoá itineraries
                _dbContext.TourItineraries.RemoveRange(tour.TourItineraries);

                // Xoá tour
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
