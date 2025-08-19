using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Services;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.PartnerControllers
{
    /// <summary>
    /// Quản lý booking (thuộc quyền sở hữu của Partner).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BookingToursController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly TripWiseDBContext _dbContext;
        private readonly IVnPayService _vnPayService;
        public BookingToursController(IBookingService bookingService, TripWiseDBContext dbContext, IVnPayService vnPayService)
        {
            _bookingService = bookingService;
            _dbContext = dbContext;
            _vnPayService = vnPayService;
        }

        /// <summary>
        /// Lấy UserId từ claim trong token.
        /// </summary>
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Lấy danh sách tất cả booking của Partner hiện tại.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPartnerBookings()
        {

            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var bookings = await _bookingService.GetBookingsByPartnerAsync(partner.PartnerId);

            return Ok(bookings);
        }

        /// <summary>
        /// Lấy chi tiết một booking theo ID.
        /// </summary>
        /// <param name="bookingId">ID của booking cần xem chi tiết.</param>
        [HttpGet("booking-detail/{bookingId}")]
        public async Task<IActionResult> GetBookingDetail(int bookingId)
        {
            var detail = await _vnPayService.GetBookingDetailAsync(bookingId);
            if (detail == null)
                return NotFound(new { Message = "Không tìm thấy booking" });

            return Ok(detail);
        }

        /// <summary>
        /// Lấy danh sách booking theo tourId (thuộc quyền sở hữu của Partner).
        /// </summary>
        /// <param name="tourId">ID của tour cần lấy danh sách booking.</param>
        [HttpGet("by-tour/{tourId}")]
        public async Task<IActionResult> GetBookingsByTourId(int tourId)
        {
            // Kiểm tra quyền partner
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn cần đăng nhập để thực hiện chức năng này.");

            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");

            // Kiểm tra tour có thuộc partner hay không
            var tour = await _dbContext.Tours
                .FirstOrDefaultAsync(t => t.TourId == tourId && t.PartnerId == partner.PartnerId);

            if (tour == null)
                return NotFound("Không tìm thấy tour hoặc tour không thuộc quyền sở hữu của bạn.");

            // Lấy danh sách bookings theo tourId
            var bookings = await _bookingService.GetBookingsByTourIdAsync(tourId);

            return Ok(bookings);
        }
    }
}
