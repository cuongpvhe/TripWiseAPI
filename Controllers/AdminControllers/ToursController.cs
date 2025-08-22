using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    /// <summary>
    /// API quản trị tour (dành cho Admin).
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [ApiController]
    [Route("api/admin/tours")]
    public class AdminToursController : ControllerBase
    {
        private readonly IManageTourService _manageTourService;
        private readonly IAIGeneratePlanService _aIGeneratePlanService;
        private readonly IVnPayService _vnPayService;
        public AdminToursController(IManageTourService manageTourService, IAIGeneratePlanService aIGeneratePlanService, IVnPayService vnPayService)
        {
            _aIGeneratePlanService = aIGeneratePlanService;
            _manageTourService = manageTourService;
            _vnPayService = vnPayService;
        }

        /// <summary>
        /// Lấy danh sách tất cả tour theo trạng thái, đối tác hoặc khoảng thời gian.
        /// </summary>
        /// <param name="status">Trạng thái của tour (Pending, Approved, Rejected...) trừ Draft.</param>
        /// <param name="partnerId">ID đối tác tạo tour.</param>
        /// <param name="fromDate">Ngày bắt đầu lọc.</param>
        /// <param name="toDate">Ngày kết thúc lọc.</param>
        [HttpGet("all-tour")]
        public async Task<IActionResult> GetAllTours([FromQuery] string? status, [FromQuery] int? partnerId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var tours = await _manageTourService.GetToursByStatusAsync(status, partnerId, fromDate, toDate);
            return Ok(tours);
        }

        /// <summary>
        /// Lấy chi tiết tour theo ID.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {
            var tour = await _manageTourService.GetTourDetailForAdminAsync(tourId);
            if (tour == null) return NotFound();
            return Ok(tour);
        }

        /// <summary>
        /// Duyệt một tour (Admin xác nhận).
        /// </summary>
        /// <param name="tourId">ID của tour cần duyệt.</param>
        [HttpPost("{tourId}/approve")]
        public async Task<IActionResult> ApproveTour(int tourId)
        {
            var adminId = GetAdminId();
            var result = await _manageTourService.ApproveTourAsync(tourId, adminId.Value);
            if (!result) return NotFound("Tour not found.");
            return Ok("Tour approved successfully.");
        }

        /// <summary>
        /// Từ chối một tour kèm lý do.
        /// </summary>
        /// <param name="tourId">ID của tour cần từ chối.</param>
        /// <param name="reason">Lý do từ chối tour.</param>
        [HttpPost("{tourId}/reject")]
        public async Task<IActionResult> RejectTour(int tourId, [FromBody] string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("Rejection reason is required.");
            var adminId = GetAdminId();
            var result = await _manageTourService.RejectTourAsync(tourId, reason, adminId.Value);
            if (!result) return NotFound("Tour not found.");
            return Ok("Tour rejected.");
        }

        /// <summary>
        /// Duyệt và cập nhật bản nháp (draft) thành tour gốc.
        /// </summary>
        /// <param name="tourId">ID tour cần cập nhật bản nháp.</param>
        [HttpPost("{tourId}/submitupdatedraft")]
        public async Task<IActionResult> SubmitUpdateDraft(int tourId)
        {
            var userId = GetAdminId();
            await _manageTourService.SubmitDraftAsync(tourId, userId.Value);
            return Ok(new { message = "Bản nháp đã được duyệt và cập nhật vào tour gốc." });
        }

        /// <summary>
        /// Lấy UserId (AdminId) từ claim trong token đăng nhập.
        /// </summary>
        private int? GetAdminId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Từ chối bản cập nhật bản nháp (draft update).
        /// </summary>
        /// <param name="tourId">ID tour liên quan đến bản nháp.</param>
        /// <param name="reason">Lý do từ chối bản nháp.</param>
        [HttpPost("reject-update")]
        public async Task<IActionResult> RejectDraftUpdate(int tourId, [FromBody] string reason)
        {
            var userId = GetAdminId(); // Lấy ID partner hiện tại
            var result = await _manageTourService.RejectDraftAsync(tourId, reason, userId.Value);
            if (!result)
                return NotFound("Không tìm thấy bản nháp tương ứng");

            return Ok("Đã từ chối bản nháp thành công");
        }

        // ============================
        // ADMIN CONFIRM REFUND
        // ============================
        [HttpPost("confirm-refund/{bookingId}")]
        public async Task<IActionResult> ConfirmRefundAsync(int bookingId)
        {
            var userId = GetAdminId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var success = await _manageTourService.ConfirmRefundAsync(bookingId, userId.Value);
            if (!success)
                return BadRequest(new { message = "Không thể duyệt hoàn tiền. Vui lòng kiểm tra trạng thái booking." });

            return Ok(new { message = "Đã duyệt hoàn tiền và gửi email thông báo cho khách + đối tác." });
        }

        // ============================
        // ADMIN COMPLETE REFUND
        // ============================
        [HttpPost("complete-refund/{bookingId}")]
        public async Task<IActionResult> CompleteRefundAsync(int bookingId)
        {
            var userId = GetAdminId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var success = await _manageTourService.CompleteRefundAsync(bookingId, userId.Value);
            if (!success)
                return BadRequest(new { message = "Không thể hoàn tất hoàn tiền. Booking chưa được duyệt hoặc không tồn tại." });

            return Ok(new { message = "Hoàn tiền thành công và đã gửi email cho khách hàng." });
        }

        // ============================
        // ADMIN REJECT REFUND
        // ============================
        [HttpPost("reject-cancelbooking/{bookingId}")]
        public async Task<IActionResult> RejectRefundAsync(int bookingId, [FromBody] RejectRefundRequest request)
        {
            var userId = GetAdminId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var success = await _manageTourService.RejectRefundAsync(bookingId, request.RejectReason, userId.Value);
            if (!success)
                return BadRequest(new { message = "Không thể từ chối hoàn tiền. Vui lòng kiểm tra trạng thái booking." });

            return Ok(new { message = "Đã từ chối yêu cầu huỷ booking và gửi email cho khách hàng." });
        }

        /// <summary>
        /// Lấy danh sách bookings cho Admin (có filter PartnerId, FromDate, ToDate).
        /// </summary>
        [HttpGet("get-booking")]
        public async Task<ActionResult<List<BookingDto>>> GetBookingsForAdmin(
            [FromQuery] int? partnerId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status)
        {
            var result = await _manageTourService.GetBookingsForAdminAsync(partnerId, fromDate, toDate, status);
            return Ok(result);
        }
        /// <summary>
        /// Lấy chi tiết booking theo ID.
        /// </summary>
        /// <param name="bookingId">ID của booking.</param>
        [HttpGet("booking-detail/{bookingId}")]
        public async Task<IActionResult> GetBookingDetail(int bookingId)
        {
            var detail = await _vnPayService.GetBookingDetailAsync(bookingId);
            if (detail == null)
                return NotFound(new { Message = "Không tìm thấy booking" });

            return Ok(detail);
        }
    }
}
