using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.Admin
{
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
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
        [HttpGet("all-partners")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _partnerService.GetAllAsync();
            return Ok(users);
        }
        [HttpGet("partners/nonactive")]
        public async Task<IActionResult> GetAllUserNonActive()
        {
            var users = await _partnerService.GetAllUserNonActiveAsync();
            return Ok(users);
        }
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

        [HttpGet("detail-partner/{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var partner = await _partnerService.GetPartnerDetailAsync(id);
            if (partner == null)
                return NotFound();
            return Ok(partner);
        }
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
    }
}
