using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers.Admin
{
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
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
        [HttpGet("All")]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _service.GetAvailablePlansAsync();
            return Ok(new { success = true, data = plans });
        }
        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var plan = await _service.GetPlanDetailAsync(id);
            if (plan == null) return NotFound("Plan not found");
            return Ok(new { success = true, data = plan });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlanCreateDto dto)
        {
            var createdBy = GetUserId();
            if (createdBy == null)
                return Unauthorized();
            var result = await _service.CreateAsync(dto, createdBy.Value);
            return Ok(new { message = "Create successfully", data = result });
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PlanUpdateDto dto)
        {
            var modifiedBy = GetUserId();
            if (modifiedBy == null)
                return Unauthorized();
            var success = await _service.UpdateAsync(id, dto, modifiedBy.Value);
            if (!success) return NotFound("Plan not found");
            return Ok(new { message = "Updated successfully", data = success });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound("Plan not found");
            return Ok(new { message = "Soft-deleted successfully" });
        }
    }
}
