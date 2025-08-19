using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models;

namespace TripWiseAPI.Controllers
{
	/// <summary>
	/// Controller quản lý các thao tác liên quan đến Blog:
	/// - Lấy danh sách blog
	/// - Lấy chi tiết blog
	/// - Tạo, cập nhật, xóa blog
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	public class BlogController : ControllerBase
	{
		private readonly IBlogService _blogService;

		public BlogController(IBlogService blogService)
		{
			_blogService = blogService;
		}

		/// <summary>
		/// Lấy danh sách tất cả các blog chưa bị xóa.
		/// </summary>
		[HttpGet("GetBlogs")]
		public async Task<IActionResult> GetBlogs()
		{
			var result = await _blogService.GetBlogsAsync();
			return Ok(new { message = "Lấy danh sách blog thành công", data = result });
		}

		/// <summary>
		/// Lấy danh sách các blog đã bị xóa.
		/// </summary>
		[HttpGet("GetBlogsDeleted")]
		public async Task<IActionResult> GetDeletedBlogs()
		{
			var result = await _blogService.GetDeletedBlogsAsync();
			return Ok(new { message = "Lấy danh sách blog đã xoá thành công", data = result });
		}

		/// <summary>
		/// Lấy thông tin chi tiết của một blog theo Id.
		/// </summary>
		/// <param name="id">Id của blog cần lấy.</param>
		[HttpGet("GetBlogById/{id}")]
		public async Task<IActionResult> GetBlogById(int id)
		{
			var blog = await _blogService.GetBlogByIdAsync(id);
			if (blog == null) return NotFound(new { message = "Không tìm thấy blog." });
			return Ok(new { message = "Thành công", data = blog });
		}

		/// <summary>
		/// Tạo một blog mới.
		/// </summary>
		/// <param name="dto">Thông tin blog cần tạo (tiêu đề, nội dung, ảnh,...).</param>
		[Authorize]
		[HttpPost("CreateBlog")]
		public async Task<IActionResult> CreateBlog([FromForm] CreateBlogDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			var blog = await _blogService.CreateBlogAsync(dto, userId);
			return Ok(new { message = "Tạo blog thành công", data = blog });
		}

		/// <summary>
		/// Cập nhật thông tin của một blog.
		/// </summary>
		/// <param name="id">Id của blog cần cập nhật.</param>
		/// <param name="dto">Thông tin blog mới.</param>
		[Authorize]
		[HttpPut("UpdateBlog/{id}")]
		public async Task<IActionResult> UpdateBlog(int id, [FromForm] CreateBlogDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			var blog = await _blogService.UpdateBlogAsync(id, dto, userId);
			if (blog == null) return NotFound(new { message = "Không tìm thấy blog." });
			return Ok(new { message = "Cập nhật thành công", data = blog });
		}

		/// <summary>
		/// Xóa một blog theo Id.
		/// </summary>
		/// <param name="id">Id của blog cần xóa.</param>
		[Authorize]
		[HttpDelete("DeleteBlog/{id}")]
		public async Task<IActionResult> DeleteBlog(int id)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			bool deleted = await _blogService.DeleteBlogAsync(id, userId);
			if (!deleted) return NotFound(new { message = "Không tìm thấy blog." });
			return Ok(new { message = "Xoá thành công", data = new { blogId = id } });
		}
	}
}
