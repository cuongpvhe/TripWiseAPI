using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    [Route("api/admin")]
    [ApiController]
    public class PartnersController : ControllerBase
    {
        private readonly IPartnerService _partnerService;

        public PartnersController(IPartnerService partnerService)
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
    }
}
