using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.PartnerServices
{
    public interface IBookingService
    {
        Task<List<BookingDto>> GetBookingsByPartnerAsync(int partnerId);
    }
}
