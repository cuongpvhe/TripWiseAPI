using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Models;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Utils;
using Microsoft.AspNetCore.WebUtilities;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Controllers
{
    /// <summary>
    /// Controller xử lý các chức năng thanh toán qua VNPay.
    /// Bao gồm mua gói dịch vụ, đặt tour, cập nhật draft booking,
    /// xác nhận thanh toán, callback từ VNPay và quản lý lịch sử thanh toán.
    /// </summary>
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

        /// <summary>
        /// Lấy UserId từ Claims của người dùng hiện tại.
        /// </summary>
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Người dùng mua gói dịch vụ (Plan).
        /// </summary>
        /// <param name="request">Thông tin yêu cầu mua gói.</param>
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

        /// <summary>
        /// Tạo draft booking cho tour trước khi thanh toán.
        /// </summary>
        /// <param name="request">Thông tin yêu cầu đặt tour.</param>
        [HttpPost("create-draft")]
        public async Task<IActionResult> CreateBookingDraft([FromBody] BuyTourRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            try
            {
                var result = await _vnPayService.CreateBookingDraftAsync(request, userId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật booking draft trước khi thanh toán.
        /// </summary>
        /// <param name="request">Thông tin cập nhật booking.</param>
        [HttpPut("update-draft")]
        public async Task<IActionResult> UpdateBookingDraft([FromBody] UpdateBookingRequest request)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            try
            {
                var result = await _vnPayService.UpdateBookingDraftAsync(request, userId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xác nhận booking và tiến hành thanh toán qua VNPay.
        /// </summary>
        /// <param name="bookingId">ID của booking cần thanh toán.</param>
        [HttpPost("confirm-and-pay/{bookingId}")]
        public async Task<IActionResult> ConfirmBookingAndPay(int bookingId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            try
            {
                var paymentUrl = await _vnPayService.ConfirmBookingAndPayAsync(bookingId, userId.Value, HttpContext);
                return Ok(new { url = paymentUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy lịch sử thanh toán của người dùng.
        /// </summary>
        /// <param name="status">Trạng thái thanh toán (tùy chọn).</param>
        [HttpGet("payment-history")]
        public async Task<IActionResult> PaymentHistory([FromQuery] string? status)
        {
            var userId = GetUserId(); // Lấy từ token hoặc session
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            var result = await _vnPayService.GetPaymentHistoryAsync(userId.Value, status);
            return Ok(result);
        }

        /// <summary>
        /// Callback từ VNPay sau khi thanh toán.
        /// </summary>
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

        /// <summary>
        /// Lấy thông báo tương ứng với mã trả về từ VNPay.
        /// </summary>
        /// <param name="code">Mã phản hồi từ VNPay.</param>
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

        /// <summary>
        /// Lấy chi tiết booking theo ID.
        /// </summary>
        /// <param name="bookingId">ID của booking.</param>
        [HttpGet("{bookingId}")]
        public async Task<IActionResult> GetBookingDetail(int bookingId)
        {
            var detail = await _vnPayService.GetBookingDetailAsync(bookingId);
            if (detail == null)
                return NotFound(new { Message = "Không tìm thấy booking" });

            return Ok(detail);
        }
    }
}