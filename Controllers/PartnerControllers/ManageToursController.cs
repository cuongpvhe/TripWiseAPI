using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Controllers.PartnerControllers
{
    /// <summary>
    /// Quản lý Tour (thuộc quyền sở hữu của Partner).
    /// </summary>
    [Authorize(Roles = "PARTNER")]
    [ApiController]
    [Route("api/partner/tours")]
    public class PartnerToursController : ControllerBase
    {
        private readonly ITourService _tourService;
        private readonly TripWiseDBContext _dbContext;
        private readonly IAIGeneratePlanService _aIGeneratePlanService;

        public PartnerToursController(ITourService tourService, TripWiseDBContext dbContext, IAIGeneratePlanService aIGeneratePlanService)
        {
            _tourService = tourService;
            _dbContext = dbContext;
            _aIGeneratePlanService = aIGeneratePlanService;
        }

        /// <summary>
        /// Lấy danh sách top địa điểm được tìm kiếm nhiều nhất.
        /// </summary>
        /// <param name="top">Số lượng địa điểm cần lấy (mặc định 10).</param>
        [HttpGet("top-destinations")]
        public async Task<IActionResult> GetTopDestinations([FromQuery] int top = 10)
        {
            var result = await _aIGeneratePlanService.GetTopSearchedDestinationsAsync(top);
            return Ok(result);
        }

        /// <summary>
        /// Lấy tất cả tour theo trạng thái hoặc ngày.
        /// </summary>
        /// <param name="status">Trạng thái tour (draft, pending, approved,...).</param>
        /// <param name="fromDate">Ngày bắt đầu lọc.</param>
        /// <param name="toDate">Ngày kết thúc lọc.</param>
        [HttpGet]
        public async Task<IActionResult> GetAllTours([FromQuery] string? status, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var tours = await _tourService.GetToursByStatusAsync(partner.PartnerId, status, fromDate, toDate);
            return Ok(tours);
        }

        /// <summary>
        /// Tạo mới một tour.
        /// </summary>
        /// <param name="request">Thông tin tour cần tạo.</param>
        [HttpPost("create-tour")]
        public async Task<IActionResult> CreateTour([FromForm] CreateTourDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var data = await _tourService.CreateTourAsync(request, partner.PartnerId);
            return Ok(new { message = "Tạo tour thành công", data });
        }


        /// <summary>
        /// Tạo lịch trình cho tour.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        /// <param name="request">Thông tin lịch trình.</param>
        [HttpPost("{tourId}/create-itinerary")]
        public async Task<IActionResult> CreateItinerary(int tourId, [FromBody] CreateItineraryDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var data = await _tourService.CreateItineraryAsync(tourId, request, partner.PartnerId);
            return Ok(new { message = "Tạo lịch trình thành công", data });
        }

        /// <summary>
        /// Tạo hoạt động trong lịch trình.
        /// </summary>
        /// <param name="itineraryId">ID lịch trình.</param>
        /// <param name="request">Thông tin hoạt động.</param>
        [HttpPost("itinerary/{itineraryId}/create-activity")]
        public async Task<IActionResult> CreateActivity(int itineraryId, [FromForm] ActivityDayDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var data = await _tourService.CreateActivityAsync(itineraryId, request, partner.PartnerId);
            return Ok(new { message = "Tạo hoạt động thành công", data });
        }

        /// <summary>
        /// Gửi tour nháp lên chờ phê duyệt.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        [HttpPost("{tourId}/submit")]
        public async Task<IActionResult> SubmitTour(int tourId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var success = await _tourService.SubmitTourAsync(tourId, partner.PartnerId);
            if (!success) return NotFound("Tour not found or access denied.");
            return Ok("Tour submitted.");
        }

        /// <summary>
        /// Lấy chi tiết một tour.
        /// </summary>
        /// <param name="tourId">ID của tour.</param>
        [HttpGet("{tourId}")]
        public async Task<IActionResult> GetTourDetail(int tourId)
        {

            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var tour = await _tourService.GetTourDetailAsync(tourId, partner.PartnerId);
            if (tour == null) return NotFound();

            return Ok(tour);
        }

        /// <summary>
        /// Cập nhật thông tin tour.
        /// </summary>
        /// <param name="tourId">ID tour cần cập nhật.</param>
        /// <param name="request">Thông tin cập nhật.</param>
        /// <param name="imageFiles">Danh sách ảnh mới tải lên từ file.</param>
        /// <param name="imageUrls">Danh sách ảnh mới tải lên từ url.</param>
        [HttpPut("update-tour/{tourId}")]
        public async Task<IActionResult> UpdateTour(
        int tourId,
        [FromForm] UpdateTourDto request,
        [FromForm] List<IFormFile>? imageFiles,
        [FromForm] List<string>? imageUrls)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.UpdateTourAsync(tourId, request, partner.PartnerId, imageFiles, imageUrls);
            if (!result) return NotFound("Tour not found");

            return Ok(new { message = "Tour updated successfully" });
        }

        /// <summary>
        /// Xoá nhiều ảnh của tour.
        /// </summary>
        /// <param name="imageIds">Danh sách ID ảnh cần xoá.</param>
        [HttpDelete("delete-multiple-images")]
        public async Task<IActionResult> DeleteMultipleImages([FromBody] List<int> imageIds)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var success = await _tourService.DeleteMultipleTourImagesAsync(imageIds, partner.PartnerId);
            if (success) return Ok("Xoá ảnh thành công.");
            return NotFound("Không tìm thấy ảnh nào để xoá.");
        }

        /// <summary>
        /// Cập nhật lịch trình.
        /// </summary>
        /// <param name="itineraryId">ID lịch trình.</param>
        /// <param name="request">Thông tin cập nhật lịch trình.</param>
        [HttpPut("update-itinerary/{itineraryId}")]
        public async Task<IActionResult> UpdateItinerary(int itineraryId, [FromBody] CreateItineraryDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.UpdateItineraryAsync(itineraryId, partner.PartnerId, request);
            if (!result) return NotFound("Itinerary not found");

            return Ok(new { message = "Itinerary updated successfully" });
        }

        /// <summary>
        /// Xoá một lịch trình.
        /// </summary>
        /// <param name="itineraryId">ID lịch trình.</param>
        [HttpDelete("delete-itinerary/{itineraryId}")]
        public async Task<IActionResult> DeleteItinerary(int itineraryId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.DeleteItineraryAsync(partner.PartnerId, itineraryId);
            if (!result) return NotFound("Itinerary not found");

            return Ok(new { message = "Itinerary deleted successfully" });
        }

        /// <summary>
        /// Cập nhật một hoạt động.
        /// </summary>
        /// <param name="activityId">ID hoạt động.</param>
        /// <param name="request">Thông tin cập nhật.</param>
        /// <param name="imageFiles">Danh sách ảnh mới tải lên từ file.</param>
        /// <param name="imageUrls">Danh sách ảnh mới tải lên từ url.</param>
        [HttpPut("update-activity/{activityId}")]
        public async Task<IActionResult> UpdateActivity(
        int activityId,
        [FromForm] UpdateActivityDto request)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var result = await _tourService.UpdateActivityAsync(activityId, partner.PartnerId, request);
            if (!result) return NotFound("Activity not found");

            return Ok(new { message = "Activity updated successfully" });
        }

        /// <summary>
        /// Xoá một hoạt động.
        /// </summary>
        /// <param name="activityId">ID hoạt động.</param>
        [HttpDelete("delete-activity/{activityId}")]
        public async Task<IActionResult> DeleteActivity(int activityId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var result = await _tourService.DeleteActivityAsync(partner.PartnerId, activityId);
            if (!result) return NotFound("Activity not found");

            return Ok(new { message = "Activity deleted successfully" });
        }

        /// <summary>
        /// Xoá nhiều ảnh của hoạt động tham quan.
        /// </summary>
        /// <param name="imageIds">Danh sách ID ảnh.</param>
        [HttpDelete("attraction/delete-multiple-images")]
        public async Task<IActionResult> DeleteMultipleAttractionImages([FromBody] List<int> imageIds)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var result = await _tourService.DeleteMultipleTourAttractionImagesAsync(imageIds, partner.PartnerId);
            return result ? Ok("Xoá thành công") : NotFound("Không tìm thấy ảnh cần xoá");
        }

        /// <summary>
        /// Xoá hoặc chuyển tour thành nháp.
        /// </summary>
        /// <param name="tourId">ID tour.</param>
        /// <param name="action">Hành động (delete hoặc draft).</param>
        [HttpDelete("delete-or-draft-tour/{tourId}")]
        public async Task<IActionResult> DeleteOrDraftTour(int tourId, [FromQuery] string action)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            var result = await _tourService.DeleteOrDraftTourAsync(tourId, action, partner.PartnerId);
            if (!result) return BadRequest("Invalid action or tour not found.");
            return Ok($"Tour {action} successful.");
        }

        /// <summary>
        /// Lấy UserId từ JWT token.
        /// </summary>
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Lấy hoặc tạo bản nháp của tour.
        /// </summary>
        /// <param name="tourId">ID tour gốc.</param>
        [HttpPost("{tourId}/create-or-get")]
        public async Task<IActionResult> GetOrCreateDraft(int tourId)
        {
            var userId = GetUserId(); // Lấy userId từ token hoặc context
            var draft = await _tourService.GetOrCreateDraftAsync(tourId);
            if (draft == null)
                return NotFound("Không tìm thấy tour gốc hoặc không thể tạo bản nháp.");

            return Ok(draft);
        }

        /// <summary>
        /// Gửi bản nháp cho admin phê duyệt.
        /// </summary>
        /// <param name="tourId">ID tour gốc.</param>
        [HttpPost("{tourId}/send-to-admin")]
        public async Task<IActionResult> SendToAdmin(int tourId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }
            await _tourService.SendDraftToAdminAsync(tourId, partner.PartnerId);
            return Ok(new { message = "Bản nháp đã được gửi cho admin phê duyệt." });
        }

        /// <summary>
        /// Gửi lại bản cập nhật tour đã bị từ chối.
        /// </summary>
        /// <param name="tourId">ID tour gốc.</param>
        [HttpPost("resubmit-rejected/{tourId}")]
        public async Task<IActionResult> ResubmitRejectedTour(int tourId)
        {
            var userId = GetUserId();

            // Lấy PartnerId từ UserId
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
            {
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");
            }

            var success = await _tourService.ResubmitRejectedDraftAsync(tourId, partner.PartnerId);
            if (!success)
                return NotFound("Không tìm thấy bản cập nhật bị từ chối");

            return Ok("Đã gửi lại bản cập nhật thành công");
        }

        /// <summary>
        /// Lấy thống kê tour của partner trong khoảng thời gian.
        /// </summary>
        /// <param name="fromDate">Ngày bắt đầu.</param>
        /// <param name="toDate">Ngày kết thúc.</param>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics(DateTime? fromDate, DateTime? toDate)
        {
            var userId = GetUserId();
            var partner = await _dbContext.Partners
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);

            if (partner == null)
                return BadRequest("Không tìm thấy Partner tương ứng với tài khoản hiện tại.");

            var stats = await _tourService.GetPartnerTourStatisticsAsync(partner.PartnerId, fromDate, toDate);
            return Ok(stats);
        }
    }
}
