using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers.Admin
{
    /// <summary>
    /// API quản lý Plan (gói dịch vụ) dành cho Admin.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [Route("api/admin/plan")]
    [ApiController]
    public class AdminPlanController : ControllerBase
    {
        private readonly IPlanService _service;
        public AdminPlanController(IPlanService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy UserId từ claims của user hiện tại.
        /// </summary>
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Lấy tất cả các Plan đang khả dụng.
        /// </summary>
        [HttpGet("All")]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _service.GetAvailablePlansAsync();
            return Ok(new { success = true, data = plans });
        }

        /// <summary>
        /// Lấy chi tiết một Plan theo ID.
        /// </summary>
        /// <param name="id">ID của Plan</param>
        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var plan = await _service.GetPlanDetailAsync(id);
            if (plan == null) return NotFound("Không tìm thấy gói.");
            return Ok(new { success = true, data = plan });
        }

        /// <summary>
        /// Tạo mới một Plan.
        /// </summary>
        /// <param name="dto">Thông tin Plan cần tạo</param>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlanCreateDto dto)
        {
            var createdBy = GetUserId();
            if (createdBy == null)
                return Unauthorized();
            var result = await _service.CreateAsync(dto, createdBy.Value);
            return Ok(new { message = "Tạo gói mới thành công", data = result });
        }

        /// <summary>
        /// Cập nhật thông tin một Plan.
        /// </summary>
        /// <param name="id">ID của Plan cần cập nhật</param>
        /// <param name="dto">Thông tin cập nhật</param>
        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PlanUpdateDto dto)
        {
            var modifiedBy = GetUserId();
            if (modifiedBy == null)
                return Unauthorized();
            var success = await _service.UpdateAsync(id, dto, modifiedBy.Value);
            if (!success) return NotFound("Không tìm thấy gói");
            return Ok(new { message = "Cập nhật gói thành công", data = success });
        }

        /// <summary>
        /// Xóa (soft-delete) một Plan theo ID.
        /// </summary>
        /// <param name="id">ID của Plan cần xóa</param>
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound("Không tìm thấy gói");
            return Ok(new { message = "Xoá mềm thành công" });
        }
    }
}
