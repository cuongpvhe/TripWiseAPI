using static TripWiseAPI.Models.DTO.UpdateTourDto;
using System;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Models;
using Microsoft.EntityFrameworkCore;
using static TripWiseAPI.Services.VnPayService;

namespace TripWiseAPI.Services
{
    public class TourUserService : ITourUserService
    {
        private readonly TripWiseDBContext _dbContext;

        public TourUserService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<PendingTourDto>> GetApprovedToursAsync()
        {
            return await _dbContext.Tours
                .Include(t => t.TourItineraries)
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .Where(t => t.Status == TourStatuses.Approved && t.RemovedDate == null)
                .Select(t => new PendingTourDto
                {
                    TourId = t.TourId,
                    TourName = t.TourName,
                    Description = t.Description,
                    Location = t.Location,
                    Price = (decimal)t.Price,
                    Status = t.Status,
                    ImageUrls = t.TourImages.Select(ti => ti.Image.ImageUrl).ToList(),
                    CreatedDate = t.CreatedDate
                })
                .ToListAsync();
        }

        public async Task<TourDetailDto?> GetApprovedTourDetailAsync(int tourId)
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
                PriceAdult = (decimal)tour.PriceAdult,
                PriceChild5To10 = (decimal)tour.PriceChild5To10,
                PriceChildUnder5 = (decimal)tour.PriceChildUnder5,
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
        public async Task<List<BookedTourDto>> GetSuccessfulBookedToursAsync(int userId)
        {
            return await _dbContext.Bookings
                .Where(b => b.UserId == userId && b.BookingStatus == "Success")
                .Include(b => b.Tour)
                .Select(b => new BookedTourDto
                {
                    BookingId = b.BookingId,
                    TourId = b.TourId,
                    TourName = b.Tour.TourName,
                    Quantity = b.Quantity,
                    TotalAmount = b.TotalAmount,
                    BookingStatus = b.BookingStatus,
                    CreatedDate = b.CreatedDate
                })
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();
        }
        public async Task<bool> AddToWishlistAsync(int userId, int tourId)
        {
            var exists = await _dbContext.Wishlists
                .AnyAsync(w => w.UserId == userId && w.TourId == tourId);

            if (exists) return false;

            var wishlist = new Wishlist
            {
                UserId = userId,
                TourId = tourId,
                DateAdded = DateTime.UtcNow
            };

            _dbContext.Wishlists.Add(wishlist);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFromWishlistAsync(int userId, int tourId)
        {
            var wishlist = await _dbContext.Wishlists
                .FirstOrDefaultAsync(w => w.UserId == userId && w.TourId == tourId);

            if (wishlist == null) return false;

            _dbContext.Wishlists.Remove(wishlist);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<PendingTourDto>> GetUserWishlistAsync(int userId)
        {
            return await _dbContext.Wishlists
                .Where(w => w.UserId == userId && w.Tour.RemovedDate == null)
                .Select(w => new PendingTourDto
                {
                    TourId = w.Tour.TourId,
                    TourName = w.Tour.TourName,
                    Description = w.Tour.Description,
                    Location = w.Tour.Location,
                    Price = (decimal)w.Tour.Price,
                    Status = w.Tour.Status,
                    ImageUrls = w.Tour.TourImages.Select(ti => ti.Image.ImageUrl).ToList(),
                    CreatedDate = w.Tour.CreatedDate
                })
                .ToListAsync();
        }

    }
}
