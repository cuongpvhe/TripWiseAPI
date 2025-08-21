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
    Task<BookingDetailDto> CreateBookingDraftAsync(BuyTourRequest request, int userId);
    Task<BookingDetailDto> UpdateBookingDraftAsync(UpdateBookingRequest request, int userId);
    Task<string> ConfirmBookingAndPayAsync(int bookingId, int userId, HttpContext context);
    //Task<string> CreateBookingAndPayAsync(BuyTourRequest request, int userId, HttpContext context);
    Task<BookingDetailDto?> GetBookingDetailAsync(int bookingId);
    Task<CancelResultDto> PreviewCancelAsync(int bookingId);
    Task<CancelResultDto> CancelBookingAsync(int bookingId, int userId, string refundMethod, string cancelReason);
}