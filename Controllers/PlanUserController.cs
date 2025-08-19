using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services;

namespace TripWiseAPI.Controllers
{
    /// <summary>
    /// Controller dành cho người dùng thao tác với các gói dịch vụ (Plan).
    /// Bao gồm lấy gói khả dụng, gói hiện tại, gói đã mua, nâng cấp gói,
    /// kiểm tra số request còn lại và số ngày dùng thử.
    /// </summary>
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

        /// <summary>
        /// Lấy danh sách các gói (Plan) khả dụng.
        /// </summary>
        [HttpGet("available")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _planService.GetAvailablePlansAsync();
            return Ok(plans);
        }

        /// <summary>
        /// Lấy gói hiện tại của người dùng theo UserId.
        /// </summary>
        /// <param name="userId">ID người dùng.</param>
        [HttpGet("current-plan/{userId}")]
        public async Task<IActionResult> GetCurrentPlan(int userId)
        {
            var plan = await _planService.GetCurrentPlanByUserIdAsync(userId);
            if (plan == null)
                return NotFound("Không tìm thấy gói cho người dùng.");

            return Ok(plan);
        }

        /// <summary>
        /// Lấy danh sách các gói mà người dùng đã mua.
        /// </summary>
        /// <param name="userId">ID người dùng.</param>
        [HttpGet("purchased/{userId}")]
        public async Task<IActionResult> GetPurchasedPlans(int userId)
        {
            var plans = await _planService.GetPurchasedPlansAsync(userId);
            return Ok(plans);
        }

        /// <summary>
        /// Nâng cấp gói dịch vụ của người dùng lên gói mới.
        /// </summary>
        /// <param name="planId">ID gói nâng cấp.</param>
        [HttpPost("upgrade/{planId}")]
        public async Task<IActionResult> UpgradePlan(int planId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Không xác định được người dùng.");

            var result = await _planService.UpgradePlanAsync(userId, planId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Lấy số lượng request còn lại của người dùng.
        /// </summary>
        /// <param name="userId">ID người dùng.</param>
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

        /// <summary>
        /// Lấy số ngày dùng thử còn lại của người dùng.
        /// </summary>
        /// <param name="userId">ID người dùng.</param>
        [HttpGet("trial-days-left/{userId}")]
        public async Task<IActionResult> GetTrialDaysLeft(int userId)
        {
            var result = await _planService.GetRemainingTrialDaysResponseAsync(userId);
            return Ok(result);
        }

    }
}
