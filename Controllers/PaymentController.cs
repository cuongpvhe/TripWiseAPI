using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Models;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Utils;
using Microsoft.AspNetCore.WebUtilities;

namespace TripWiseAPI.Controllers
{
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IVnPayService _vnPayService;
        private readonly IConfiguration _configuration;
        private readonly TripWiseDBContext _dbContext;

        public PaymentController(IVnPayService vnPayService, IConfiguration configuration, TripWiseDBContext dbContext)
        {
            _vnPayService = vnPayService;
            _configuration = configuration;
            _dbContext = dbContext;
        }   

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        [HttpPost("buy-plan")]
        public async Task<IActionResult> BuyPlan([FromBody] BuyPlanRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            try
            {
                var paymentUrl = await _vnPayService.BuyPlanAsync(request, userId.Value, HttpContext);
                return Ok(new { url = paymentUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("booking")]
        public async Task<IActionResult> PayBooking([FromBody] BuyTourRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            try
            {
                var paymentUrl = await _vnPayService.CreateBookingAndPayAsync(request, userId.Value, HttpContext);
                return Ok(new { url = paymentUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        [HttpGet("callback")]
        public async Task<IActionResult> PaymentCallback()
        {
            await _vnPayService.HandlePaymentCallbackAsync(Request.Query);

            var vnp_ResponseCode = Request.Query["vnp_ResponseCode"].ToString();
            var vnp_TransactionNo = Request.Query["vnp_TransactionNo"].ToString();

            var message = GetVnpMessage(vnp_ResponseCode);

            var queryParams = new Dictionary<string, string>
            {
                 { "success", vnp_ResponseCode == "00" ? "true" : "false" },
                 { "message", message },
                 { "transactionId", vnp_TransactionNo }
            };

            var url = QueryHelpers.AddQueryString("http://localhost:5175/vnpay-callback", queryParams);

            return Redirect(url);
        }
        private string GetVnpMessage(string code)
        {
            return code switch
            {
                "00" => "Thanh toán thành công",
                "01" => "Giao dịch không thành công",
                "02" => "Giao dịch bị từ chối",
                "24" => "Giao dịch bị hủy",
                _ => "Không xác định"
            };
        }


    }
}