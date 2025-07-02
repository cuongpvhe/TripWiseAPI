using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/plan")]
    public class PlanUserController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlanUserController(IPlanService planService)
        {
            _planService = planService;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _planService.GetAvailablePlansAsync();
            return Ok(plans);
        }
        [HttpGet("current-plan/{userId}")]
        public async Task<IActionResult> GetCurrentPlan(int userId)
        {
            var plan = await _planService.GetCurrentPlanByUserIdAsync(userId);
            if (plan == null)
                return NotFound("Không tìm thấy gói cho người dùng.");

            return Ok(plan);
        }
        [HttpGet("purchased/{userId}")]
        public async Task<IActionResult> GetPurchasedPlans(int userId)
        {
            var plans = await _planService.GetPurchasedPlansAsync(userId);
            return Ok(plans);
        }


        [HttpPost("upgrade/{planId}")]
        public async Task<IActionResult> UpgradePlan(int planId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Không xác định được người dùng.");

            var result = await _planService.UpgradePlanAsync(userId, planId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("requests-remaining/{userId}")]
        public async Task<IActionResult> GetRemainingRequests(int userId)
        {
            try
            {
                int remaining = await _planService.GetRemainingRequestsAsync(userId);
                return Ok(new { UserId = userId, RemainingRequests = remaining });
            }
            catch (Exception ex)
            {
                return NotFound(new { Message = ex.Message });
            }
        }
        [HttpGet("trial-days-left/{userId}")]
        public async Task<IActionResult> GetTrialDaysLeft(int userId)
        {
            var result = await _planService.GetRemainingTrialDaysResponseAsync(userId);
            return Ok(result);
        }

    }
}
