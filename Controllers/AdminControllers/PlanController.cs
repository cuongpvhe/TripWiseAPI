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
    [ApiController]
    [Route("admin/plan")]
    
    public class PlanController : ControllerBase
    {
        private readonly IPlanService _service;


        public PlanController(IPlanService service)
        {
            _service = service;
        }
        [HttpGet("AllPlans")]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _service.GetAvailablePlansAsync();
            return Ok(new { success = true, data = plans });
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var plan = await _service.GetByIdAsync(id);
            if (plan == null) return NotFound("Plan not found");
            return Ok(new { success = true, data = plan });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlanCreateDto dto)
        {
            var result = await _service.CreateAsync(dto);
            return Ok(new { message = "Create successfully", data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PlanUpdateDto dto)
        {
            var success = await _service.UpdateAsync(id, dto);
            if (!success) return NotFound("Plan not found");
            return Ok(new { message = "Updated successfully", data = success });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound("Plan not found");
            return Ok(new { message = "Soft-deleted successfully" });
        }
    }
}
