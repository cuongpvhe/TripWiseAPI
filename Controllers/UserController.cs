using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers
{
    /// <summary>
    /// Controller quản lý hồ sơ người dùng (USER).
    /// - Lấy thông tin hồ sơ
    /// - Cập nhật hồ sơ
    /// </summary>
    [Authorize(Roles = "USER")]
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserProfileService _userService;

        public UserController(IUserProfileService userService)
        {
            _userService = userService;
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
        /// Lấy thông tin hồ sơ người dùng hiện tại.
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var profile = await _userService.GetProfileAsync(userId.Value);
            if (profile == null)
                return NotFound("Không tìm thấy người dùng hoặc tài khoản đã bị khóa.");

            return Ok(profile);
        }

        /// <summary>
        /// Cập nhật thông tin hồ sơ người dùng.
        /// </summary>
        /// <param name="dto">Thông tin hồ sơ cần cập nhật.</param>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UserProfileUpdateDTO dto)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState); 

            var success = await _userService.UpdateProfileAsync(userId.Value, dto);
            if (!success)
                return NotFound("Không tìm thấy người dùng hoặc tài khoản đã bị khóa.");

            return Ok(new { message = "Cập nhật hồ sơ thành công." });
        }


    }
}
