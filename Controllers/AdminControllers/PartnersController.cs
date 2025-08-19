using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.Admin
{
    /// <summary>
    /// Quản lý các đối tác (Partner) dành cho Admin.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [Route("api/admin")]
    [ApiController]
    public class AdminPartnersController : ControllerBase
    {
        private readonly IPartnerService _partnerService;
        public AdminPartnersController(IPartnerService partnerService)
        {
            _partnerService = partnerService;
        }

        /// <summary>
        /// Lấy UserId từ claims của người dùng hiện tại.
        /// Trả về null nếu không tồn tại hoặc không hợp lệ.
        /// </summary>
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Lấy danh sách tất cả đối tác.
        /// </summary>
        [HttpGet("all-partners")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _partnerService.GetAllAsync();
            return Ok(users);
        }

        /// <summary>
        /// Lấy danh sách đối tác chưa được kích hoạt.
        /// </summary>
        [HttpGet("partners/nonactive")]
        public async Task<IActionResult> GetAllUserNonActive()
        {
            var users = await _partnerService.GetAllUserNonActiveAsync();
            return Ok(users);
        }

        /// <summary>
        /// Tạo tài khoản đối tác mới.
        /// </summary>
        [HttpPost("create-partner")]
        public async Task<IActionResult> CreatePartner([FromBody] CreatePartnerAccountDto dto)
        {
            var createdBy = GetUserId();
            if (createdBy == null)
                return Unauthorized();
            var result = await _partnerService.CreatePartnerAccountAsync(dto, createdBy.Value);
            if (!result)
                return BadRequest("Email đã tồn tại hoặc lỗi khi tạo.");

            return Ok(new { message = "Tạo tài khoản đối tác thành công." });
        }

        /// <summary>
        /// Xóa đối tác theo ID.
        /// </summary>
        [HttpDelete("delete-partner/{id}")]
        public async Task<IActionResult> DeleteUser(int id, [FromBody] string removedReason)
        {
            var removedBy = GetUserId();
            if (removedBy == null)
                return Unauthorized();
            var result = await _partnerService.DeleteAsync(id, removedBy.Value, removedReason);
            if (!result)
                return NotFound("Không tìm thấy người dùng.");

            return Ok(new { message = "Xóa người dùng thành công." });
        }

        /// <summary>
        /// Kích hoạt lại đối tác theo ID.
        /// </summary>
        [HttpPut("activate-partner/{id}")]
        public async Task<IActionResult> Activate(int id)
        {
            var modifiedBy = GetUserId();
            if (modifiedBy == null)
                return Unauthorized();
            var result = await _partnerService.SetActiveStatusAsync(id, modifiedBy.Value);
            if (!result)
                return NotFound("Không tìm thấy đối tác.");

            return Ok(new { message = "Kích hoạt đối tác thành công." });
        }

        /// <summary>
        /// Lấy chi tiết thông tin đối tác theo ID.
        /// </summary>
        [HttpGet("detail-partner/{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var partner = await _partnerService.GetPartnerDetailAsync(id);
            if (partner == null)
                return NotFound();
            return Ok(partner);
        }

        /// <summary>
        /// Cập nhật thông tin đối tác theo ID.
        /// </summary>
        [HttpPut("update-partner/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PartnerUpdatelDto dto)
        {
            var modifiedBy = GetUserId();
            if (modifiedBy == null)
                return Unauthorized();
            var result = await _partnerService.UpdateAsync(id, dto, modifiedBy.Value);
            if (!result)
                return NotFound("Không tìm thấy đối tác.");

            return Ok(new { message = "Cập nhật thông tin thành công." });
        }

        /// <summary>
        /// Lấy danh sách review của một đối tác theo PartnerId.
        /// </summary>
        [HttpGet("partner/{partnerId}/reviews")]
        public async Task<IActionResult> GetReviewsByPartner(int partnerId)
        {
            var result = await _partnerService.GetTourReviewsByPartnerAsync(partnerId);
            return Ok(result);
        }

        /// <summary>
        /// Lấy điểm đánh giá trung bình của một đối tác theo PartnerId.
        /// </summary>
        [HttpGet("partner/{partnerId}/average-rating")]
        public async Task<IActionResult> GetAverageRatingForPartner(int partnerId)
        {
            var avgRating = await _partnerService.GetAverageRatingByPartnerAsync(partnerId);
            return Ok(new { PartnerId = partnerId, AverageRating = avgRating });
        }
    }
}
