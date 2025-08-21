using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.APIModel;

namespace TripWiseAPI.Controllers
{
    /// <summary>
    /// Controller quản lý log trong hệ thống.
    /// Cung cấp các chức năng:
    /// - Kiểm tra kết nối Firebase
    /// - Lấy log có phân trang
    /// - Xóa log thủ công
    /// - Dọn dẹp log hết hạn
    /// </summary>
    [ApiController]
	[Route("api/logs")]
	public class LogsController : ControllerBase
	{
		private readonly FirebaseLogService _logService;

		public LogsController(FirebaseLogService logService)
		{
			_logService = logService;
		}

        //// <summary>
        /// Kiểm tra tình trạng kết nối đến Firebase.
        /// </summary>
        [HttpGet("Check")]
		public async Task<IActionResult> Health()
		{
			if (!await _logService.PingAsync())
				return StatusCode(502, "Không kết nối được tới Firebase.");
			return Ok("Firebase reachable");
		}

        /// <summary>
        /// Lấy danh sách log đã được lọc và phân trang.
        /// </summary>
        /// <param name="page">Trang hiện tại (mặc định = 1).</param>
        /// <param name="pageSize">Số lượng log trên mỗi trang (mặc định = 1000).</param>
        [HttpGet("filtered")]
		public async Task<IActionResult> GetFilteredLogs(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 1000)
		{
			try
			{
				// Lấy raw logs từ Firebase
				var raw = await _logService.GetRawLogsAsync();

				// Chuyển sang APIResponseLogs và lấy DateTime hợp lệ
				var filtered = raw.Select(log =>
				{
					var dateTime = log.CreatedDate ?? log.ModifiedDate ?? log.RemovedDate;
					return new APIResponseLogs
					{
						Id = log.Id,
						UserId = log.UserId,
						UserName = log.UserName,
						Action = log.Action,
						Message = log.Message,
						StatusCode = log.StatusCode,
						DateTime = dateTime
					};
				})
				.Where(r => r.DateTime.HasValue) // chỉ lấy log có DateTime
				.OrderByDescending(r => r.DateTime.Value) // sắp xếp theo thời gian giảm dần
				.ToList();

				// Phân trang
				var total = filtered.Count;
				var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

				return Ok(new { Total = total, Page = page, PageSize = pageSize, Data = paged });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

        /// <summary>
        /// Xóa thủ công một log theo ID.
        /// </summary>
        /// <param name="id">ID của log cần xóa.</param>
        [HttpDelete("{id:int}")]
		public async Task<IActionResult> DeleteLog(int id)
		{
			await _logService.DeleteLogByIdAsync(id);
			return Ok(new { message = $"Deleted log {id}" });
		}

        /// <summary>
        /// Dọn dẹp ngay các log đã hết hạn trong Firebase.
        /// </summary>
        [HttpPost("cleanup")]
		public async Task<IActionResult> CleanupExpired()
		{
			await _logService.CleanupExpiredLogsAsync();
			return Ok(new { message = "Expired logs cleaned up." });
		}
	}
}
