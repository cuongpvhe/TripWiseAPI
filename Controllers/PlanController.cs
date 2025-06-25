using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers
{
    [ApiController]
    [Route("api/plan")]
    public class PlanController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlanController(IPlanService planService)
        {
            _planService = planService;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _planService.GetAvailablePlansAsync();
            return Ok(plans);
        }

        [Authorize]
        [HttpPost("upgrade/{planId}")]
        public async Task<IActionResult> UpgradePlan(int planId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Không xác định được người dùng.");

            var result = await _planService.UpgradePlanAsync(userId, planId);
            return StatusCode(result.StatusCode, result);
        }
    }
}
