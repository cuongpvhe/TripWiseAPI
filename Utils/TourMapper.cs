using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Utils
{
    public static class TourMapper
    {
        public static RelatedTourDto ToRelatedDto(Tour tour)
        {
            return new RelatedTourDto
            {
                TourId = tour.TourId,
                TourName = tour.TourName,
                Description = tour.Description,
                Price = tour.Price,
                Duration = tour.Duration,
                Location = tour.Location,
                Thumbnail = tour.TourImages?
                    .Select(ti => ti.Image?.ImageUrl)
                    .FirstOrDefault(url => !string.IsNullOrEmpty(url))
            };
        }
    }
}
