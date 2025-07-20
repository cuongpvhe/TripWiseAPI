using static TripWiseAPI.Models.DTO.UpdateTourDto;
using System;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Models;
using Microsoft.EntityFrameworkCore;

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
                .Include(t => t.TourImages).ThenInclude(ti => ti.Image)
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.Status == TourStatuses.Approved && t.RemovedDate == null);

            if (tour == null) return null;

            var itineraryDtos = new List<ItineraryDto>();

            foreach (var itinerary in tour.TourItineraries.OrderBy(i => i.DayNumber))
            {
                var attractions = await _dbContext.TourAttractions
                    .Where(a => a.ItineraryId == itinerary.ItineraryId)
                    .ToListAsync();

                itineraryDtos.Add(new ItineraryDto
                {
                    DayNumber = itinerary.DayNumber,
                    Title = itinerary.ItineraryName,
                    DailyCost = attractions.Sum(x => x.Price ?? 0),
                    Activities = attractions.Select(a => new ActivityDto
                    {
                        StartTime = a.StartTime ?? TimeSpan.Zero,
                        EndTime = a.EndTime ?? TimeSpan.Zero,
                        Description = a.TourAttractionsName,
                        Address = a.Localtion,
                        EstimatedCost = a.Price,
                        PlaceDetail = a.Description,
                        MapUrl = a.MapUrl,
                        ImageUrls = new List<string> { a.ImageUrl }
                    }).ToList()
                });
            }

            var imageUrls = tour.TourImages
                .Where(ti => ti.Image != null && ti.Image.RemovedDate == null)
                .Select(ti => ti.Image.ImageUrl)
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();

            return new TourDetailDto
            {
                TourName = tour.TourName,
                Description = tour.Description,
                Location = tour.Location,
                TravelDate = tour.CreatedDate,
                Days = tour.Duration,
                Preferences = tour.Category,
                Budget = null,
                TotalEstimatedCost = tour.Price,
                PricePerDay = tour.PricePerDay,
                TourInfo = tour.TourInfo,
                TourNote = tour.TourNote,
                Itinerary = itineraryDtos,
                Status = tour.Status,
                RejectReason = tour.RejectReason,
                ImageUrls = imageUrls
            };
        }
    }
}
