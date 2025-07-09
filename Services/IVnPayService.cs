using Microsoft.AspNetCore.Http;
using TripWiseAPI.Models;

namespace TripWiseAPI.Services;
public interface IVnPayService
{
    string CreatePaymentUrl(PaymentInformationModel model, HttpContext context);
    Task<string> HandlePaymentCallbackAsync(IQueryCollection query);
}