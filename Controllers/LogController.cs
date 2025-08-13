using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.APIModel;

namespace TripWiseAPI.Controllers
{
	[ApiController]
	[Route("api/logs")]
	public class LogsController : ControllerBase
	{
		private readonly FirebaseLogService _logService;

		public LogsController(FirebaseLogService logService)
		{
			_logService = logService;
		}

		// Health check kết nối Firebase
		[HttpGet("Check")]
		public async Task<IActionResult> Health()
		{
			if (!await _logService.PingAsync())
				return StatusCode(502, "Không kết nối được tới Firebase.");
			return Ok("Firebase reachable");
		}

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




		// Xoá thủ công một log
		[HttpDelete("{id:int}")]
		public async Task<IActionResult> DeleteLog(int id)
		{
			await _logService.DeleteLogByIdAsync(id);
			return Ok(new { message = $"Deleted log {id}" });
		}

		// Dọn các log hết hạn ngay
		[HttpPost("cleanup")]
		public async Task<IActionResult> CleanupExpired()
		{
			await _logService.CleanupExpiredLogsAsync();
			return Ok(new { message = "Expired logs cleaned up." });
		}
	}
}
