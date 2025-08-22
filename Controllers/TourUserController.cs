using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers
{
    /// <summary>
    /// Controller quản lý các chức năng tour dành cho người dùng:
    /// - Xem tour đã phê duyệt
    /// - Đặt tour và xem chi tiết booking
    /// - Quản lý danh sách yêu thích
    /// - Lấy tin tức (HotNews)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TourUserController : ControllerBase
    {
        private readonly ITourUserService _tourUserService;
        private readonly IVnPayService _vnPayService;
        private readonly IAppSettingsService _service;
        public TourUserController(ITourUserService tourUserService, IVnPayService vnPayService, IAppSettingsService appSettingsService)
        {
            _tourUserService = tourUserService;
            _vnPayService = vnPayService;
            _service = appSettingsService;
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
        /// Lấy danh sách tour đã được phê duyệt.
        /// </summary>
        [HttpGet("approved")]
        public async Task<IActionResult> GetApprovedTours()
        {
            var tours = await _tourUserService.GetApprovedToursAsync();
            return Ok(tours);
        }


        /// <summary>
        /// Lấy chi tiết tour đã được phê duyệt theo ID.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        [HttpGet("approved/{tourId}")]
        public async Task<IActionResult> GetApprovedTourDetail(int tourId)
        {
            var detail = await _tourUserService.GetApprovedTourDetailAsync(tourId);
            if (detail == null)
                return NotFound("Không tìm thấy tour.");
            return Ok(detail);
        }

        /// <summary>
        /// Lấy danh sách tour mà người dùng đã đặt thành công.
        /// </summary>
        [HttpGet("booked-tours")]
        public async Task<IActionResult> GetBookedTours()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            var result = await _tourUserService.GetBookedToursWithCancelledAsync(userId.Value);
            return Ok(result);
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

        /// <summary>
        /// Thêm tour vào danh sách yêu thích.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        [HttpPost("addWishlist")]
        public async Task<IActionResult> AddToWishlist(int tourId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var result = await _tourUserService.AddToWishlistAsync(userId.Value, tourId);
            if (!result) return BadRequest("Tour đã có trong danh sách yêu thích.");
            return Ok("Đã thêm vào danh sách yêu thích.");
        }

        /// <summary>
        /// Xóa tour khỏi danh sách yêu thích.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        [HttpDelete("removeFromWishlist")]
        public async Task<IActionResult> RemoveFromWishlist( int tourId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var result = await _tourUserService.RemoveFromWishlistAsync(userId.Value, tourId);
            if (!result) return NotFound("Không tìm thấy tour trong danh sách yêu thích.");
            return Ok("Đã xoá khỏi danh sách yêu thích.");
        }

        /// <summary>
        /// Lấy danh sách tour yêu thích của người dùng.
        /// </summary>
        [HttpGet("Wishlist")]
        public async Task<IActionResult> GetUserWishlist()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var tours = await _tourUserService.GetUserWishlistAsync(userId.Value);
            return Ok(tours);
        }

        /// <summary>
        /// Lấy danh sách HotNews
        /// </summary>
        [HttpGet("hot-new")]
        public async Task<IActionResult> GetAllHotNew()
        {
            var list = await _service.GetAllHotNewAsync();
            return Ok(list);
        }

        /// <summary>
        /// Lấy chi tiết HotNews theo ID.
        /// </summary>
        /// <param name="id">ID của HotNews.</param>
        [HttpGet("hot-new-by/{id:int}")]
        public async Task<IActionResult> GetByIdHotNew(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(new { Message = "Không tìm thấy HotNews" });

            return Ok(item);
        }
    }
}
