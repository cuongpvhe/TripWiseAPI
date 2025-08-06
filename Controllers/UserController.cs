using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers
{
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

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

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
