using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;


namespace TripWiseAPI.Controllers.Admin
{
    [Authorize(Roles = "ADMIN")]
    [Route("api/admin")]
    [ApiController]
    public class AdminUsersController : ControllerBase
    {
        private readonly IUserService _service;

        public AdminUsersController(IUserService service)
        {
            _service = service;
        }
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
        [HttpGet("allusers")]
        public async Task<IActionResult> GetAll()
        {
            var users = await _service.GetAllAsync();
            return Ok(users);
        }
        [HttpGet("users/nonactive")]
        public async Task<IActionResult> GetAllUserNonActive()
        {
            var users = await _service.GetAllUserNonActiveAsync();
            return Ok(users);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
        {
            var createdBy = GetUserId();
            if (createdBy == null)
                return Unauthorized();

            var result = await _service.CreateUserAsync(dto, createdBy.Value);
            if (!result)
                return BadRequest("Email đã tồn tại hoặc lỗi khi tạo.");

            return Ok(new { message = "Tạo người dùng thành công." });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id, [FromBody] string removedReason)
        {
            var removedBy = GetUserId();
            if (removedBy == null)
                return Unauthorized();

            var result = await _service.DeleteAsync(id, removedBy.Value, removedReason);
            if (!result)
                return NotFound("Không tìm thấy người dùng.");

            return Ok(new { message = "Xóa người dùng thành công." });
        }


        [HttpPut("{userId}/activate")]
        public async Task<IActionResult> ReactivateUser(int userId)
        {
            var result = await _service.SetActiveStatusAsync(userId);
            if (!result)
                return NotFound("User not found.");

            return Ok(new { message = "User has been reactivated." });
        }

        [HttpGet("user-detail/{userId}")]
        public async Task<IActionResult> GetUserDetail(int userId)
        {
            var user = await _service.GetUserDetailAsync(userId);
            if (user == null)
                return NotFound();

            return Ok(user);
        }
        [HttpPut("update/{userId}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UserUpdatelDto dto)
        {
            var modifiedBy = GetUserId();
            if (modifiedBy == null)
                return Unauthorized();

            var result = await _service.UpdateAsync(userId, dto, modifiedBy.Value);
            if (!result)
                return NotFound("User not found or has been removed.");

            return Ok(new { message = "User updated successfully." });
        }

    }
}
