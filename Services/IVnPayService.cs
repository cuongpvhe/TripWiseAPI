using Microsoft.AspNetCore.Http;
using TripWiseAPI.Models;

namespace TripWiseAPI.Services;
public interface IVnPayService
{
    string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
    Task HandlePaymentCallbackAsync(IQueryCollection query);
    Task<string> BuyPlanAsync(BuyPlanRequest request, int userId, HttpContext context);
    Task<string> CreateBookingAndPayAsync(BuyTourRequest request, int userId, HttpContext context);
}