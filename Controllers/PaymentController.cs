using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Models;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Utils;

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
            try
            {
                var result = await _vnPayService.HandlePaymentCallbackAsync(Request.Query);

                return Ok(new
                {
                    status = result, // "Success", "Canceled", "Failed"
                    timestamp = TimeHelper.GetVietnamTime()
            });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = "Failed",
                    error = ex.Message
                });
            }
        }

    }
}