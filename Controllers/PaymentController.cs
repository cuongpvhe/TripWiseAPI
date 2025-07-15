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

            var plan = await _dbContext.Plans
                .FirstOrDefaultAsync(p => p.PlanId == request.PlanId && p.RemovedDate == null);

            if (plan == null)
                return NotFound("Gói cước không tồn tại.");

            if (plan.Price == null || plan.Price <= 0)
                return BadRequest("Gói cước không hợp lệ.");

            var paymentModel = new PaymentInformationModel
            {
                UserId = userId.Value,
                Amount = (decimal)plan.Price,
                Name = $"Plan: {plan.PlanName}",
                OrderDescription = $"Thanh toán gói {plan.PlanName} giá {plan.Price:N0} VND",
                OrderType = "plan",
                PlanId = plan.PlanId
            };

            var paymentUrl = _vnPayService.CreatePaymentUrl(paymentModel, HttpContext);

            return Ok(new { url = paymentUrl });
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