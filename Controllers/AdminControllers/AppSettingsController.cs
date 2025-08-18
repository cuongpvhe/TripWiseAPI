using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers.Admin
{    
    [Authorize(Roles = "ADMIN")]
    [Route("api/admin/appsettings")]
    [ApiController]
    
    public class AdminAppSettingsController : ControllerBase
    {
        private readonly IAppSettingsService _service;
        private readonly IImageUploadService _imageUploadService;
        public AdminAppSettingsController(IAppSettingsService service, IImageUploadService imageUploadService)
        {
            _service = service;
            _imageUploadService = imageUploadService;
        }
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
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


        /// <summary>
        /// Lấy danh sách HotNews
        /// </summary>
        [HttpGet("hot-new")]
        public async Task<IActionResult> GetAllHotNew()
        {
            var list = await _service.GetAllHotNewAsync();
            return Ok(list);
        }

        /// <summary>
        /// Lấy chi tiết HotNews theo Id
        /// </summary>
        [HttpGet("hot-new-by/{id:int}")]
        public async Task<IActionResult> GetByIdHotNew(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(new { Message = "Không tìm thấy HotNews" });

            return Ok(item);
        }

        /// <summary>
        /// Thêm mới HotNews
        /// </summary>
        [HttpPost("create-hot-new")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> CreateHotNew([FromForm] HotNewsRequest request)
        {
            var createdBy = GetUserId()?.ToString() ?? "system";
            var id = await _service.CreateAsync(request, createdBy);
            return Ok(new { Id = id, Message = "Tạo HotNews thành công" });
        }

        /// <summary>
        /// Cập nhật HotNews theo Id
        /// </summary>
        [HttpPut("hot-new-update/{id:int}")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UpdateHotNew(int id, [FromForm] HotNewsRequest request)
        {
            var modifiedBy = GetUserId()?.ToString() ?? "system";
            var success = await _service.UpdateAsync(id, request, modifiedBy);
            if (!success) return NotFound(new { Message = "Không tìm thấy HotNews cần cập nhật" });

            return Ok(new { Message = "Cập nhật HotNews thành công" });
        }

        /// <summary>
        /// Xoá (soft delete) HotNews theo Id
        /// </summary>
        [HttpDelete("hot-new-delete/{id:int}")]
        public async Task<IActionResult> DeleteHotNew(int id, [FromQuery] string reason = "Không rõ")
        {
            var removedBy = GetUserId()?.ToString() ?? "system";
            var success = await _service.DeleteAsync(id, removedBy, reason);
            if (!success) return NotFound(new { Message = "Không tìm thấy HotNews cần xoá" });

            return Ok(new { Message = "Đã đánh dấu xoá HotNews thành công", Reason = reason });
        }


    }

}
