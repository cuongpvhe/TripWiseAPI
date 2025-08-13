using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.Admin
{    
    [Authorize(Roles = "ADMIN")]
    [Route("api/admin/appsettings")]
    [ApiController]
    
    public class AdminAppSettingsController : ControllerBase
    {
        private readonly IAppSettingsService _service;

        public AdminAppSettingsController(IAppSettingsService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var settings = await _service.GetAllAsync();
            return Ok(settings);
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetByKey(string key)
        {
            var setting = await _service.GetByKeyAsync(key);
            if (setting == null) return NotFound();
            return Ok(setting);
        }

        [HttpPut("{key}")]
        public async Task<IActionResult> Update(string key, [FromBody] AppSetting dto)
        {
            if (key != dto.Key) return BadRequest("Key mismatch");

            var success = await _service.UpdateAsync(dto);
            if (!success) return NotFound();

            return Ok(new { message = "Cập nhật thành công." });
        }
        [HttpPost("set-free-plan")]
        public async Task<IActionResult> SetFreePlan([FromBody] string planName)
        {
            var success = await _service.SetFreePlanAsync(planName);
            if (!success)
                return NotFound(new { success = false, message = "Không tìm thấy gói hợp lệ." });

            return Ok(new { success = true, message = "Đã đặt gói này làm gói Free." });
        }
        [HttpPost("set-trial-plan")]
        public async Task<IActionResult> SetTrialPlan([FromBody] string planName)
        {
            var success = await _service.SetTrialPlanAsync(planName);
            if (!success)
                return NotFound(new { success = false, message = "Không tìm thấy gói hợp lệ." });

            return Ok(new { success = true, message = "Đã đặt gói này làm gói Trial." });
        }
        [HttpPut("trial-duration")]
        public async Task<IActionResult> UpdateTrialDuration([FromBody] int days)
        {
            bool success = await _service.SetValueAsync("TrialDurationInDays", days.ToString());

            if (!success) return StatusCode(500, "Cập nhật thất bại.");
            return Ok(new { message = $"Đã cập nhật TrialDurationInDays = {days}" });
        }

        [HttpGet("timeout")]
        public async Task<IActionResult> GetTimeout()
        {
            var timeout = await _service.GetTimeoutAsync();
            return Ok(new { TimeoutMinutes = timeout });
        }

        [HttpPost("upadte-timeout")]
        public async Task<IActionResult> UpdateTimeout([FromBody] int minutes)
        {
            await _service.UpdateTimeoutAsync(minutes);
            return Ok(new { Message = "Đã cập nhật thời gian hết hạn phiên thành công", TimeoutMinutes = minutes });
        }
        /// <summary>
        /// Lấy thời gian timeout của OTP
        /// </summary>
        [HttpGet("otp-timeout")]
        public async Task<IActionResult> GetOtpTimeout()
        {
            var timeout = await _service.GetOtpTimeoutAsync();
            if (!timeout.HasValue)
                return NotFound(new { Message = "Chưa cấu hình thời gian OTP" });

            return Ok(new { TimeoutSeconds = timeout.Value });
        }

        /// <summary>
        /// Cập nhật thời gian timeout của OTP
        /// </summary>
        [HttpPut("otp-timeout")]
        public async Task<IActionResult> UpdateOtpTimeout([FromBody] int timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
                return BadRequest(new { Message = "Thời gian OTP phải lớn hơn 0" });

            var success = await _service.UpdateOtpTimeoutAsync(timeoutSeconds);
            if (!success)
                return BadRequest(new { Message = "Cập nhật thất bại" });

            return Ok(new { Message = "Cập nhật thời gian OTP thành công" });
        }

    }

}
