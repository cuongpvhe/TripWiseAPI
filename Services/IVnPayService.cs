using Microsoft.AspNetCore.Http;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services;
public interface IVnPayService
{
    string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
    Task<List<PaymentTransactionDto>> GetPaymentHistoryAsync(int userId, string? status);
    Task HandlePaymentCallbackAsync(IQueryCollection query);
    Task<string> BuyPlanAsync(BuyPlanRequest request, int userId, HttpContext context);
    Task<string> CreateBookingAndPayAsync(BuyTourRequest request, int userId, HttpContext context);
    Task<BookingDetailDto?> GetBookingDetailAsync(int bookingId);
}