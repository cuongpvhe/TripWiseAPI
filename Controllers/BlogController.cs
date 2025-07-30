using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services; // Thêm namespace của service

namespace TripWiseAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class BlogController : ControllerBase
	{
		private readonly IBlogService _blogService; // Thay thế DbContext và IConfiguration bằng service

		public BlogController(IBlogService blogService) // Inject service
		{
			_blogService = blogService ?? throw new ArgumentNullException(nameof(blogService));
		}

		[HttpGet("GetBlogs")]
		public async Task<ActionResult<IEnumerable<BlogDto>>> GetBlogs()
		{
			var blogs = await _blogService.GetBlogsAsync();
			if (blogs == null || !blogs.Any())
			{
				return NotFound(new { message = "Không có bài blog nào." });
			}
			return Ok(new
			{
				message = "Lấy danh sách blog thành công.",
				data = blogs
			});
		}


		[HttpGet("GetBlogsDeleted")]
		public async Task<ActionResult<IEnumerable<BlogDto>>> GetBlogsDelete()
		{
			var blogs = await _blogService.GetBlogsDeletedAsync();
			if (blogs == null || !blogs.Any())
			{
				return NotFound(new { message = "Không có bài blog nào." });
			}
			return Ok(new
			{
				message = "Lấy danh sách blog đã xóa thành công.", // Sửa message cho rõ ràng
				data = blogs
			});
		}

		[HttpGet("GetBlogById/{id}")]
		public async Task<ActionResult<BlogDto>> GetBlog(int id)
		{
			var blog = await _blogService.GetBlogByIdAsync(id);
			if (blog == null)
			{
				return NotFound(new { message = "Không tìm thấy blog." });
			}
			return Ok(new
			{
				message = "Lấy thông tin blog thành công.", // Sửa message cho rõ ràng
				data = blog
			});
		}

		[Authorize]
		[HttpPost("CreateBlog")]
		public async Task<IActionResult> CreateBlog([FromForm] CreateBlogDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			int? userId = int.TryParse(userIdClaim, out int parsedId) ? parsedId : null;

			// Kiểm tra nếu không có ảnh upload và không có URL
			bool noUploadedImages = dto.Images == null || !dto.Images.Any(i => i?.Length > 0);
			bool noImageUrls = dto.ImageUrls == null || !dto.ImageUrls.Any(u => !string.IsNullOrWhiteSpace(u));

			if (noUploadedImages && noImageUrls)
			{
				return BadRequest(new
				{
					message = "Vui lòng tải lên ít nhất một ảnh hoặc cung cấp URL ảnh."
				});
			}

			var result = await _blogService.CreateBlogAsync(dto, userId);

			return Ok(new
			{
				message = "Tạo blog thành công.",
				data = result
			});
		}


		[Authorize]
		[HttpPut("UpdateBlog/{id}")]
		public async Task<IActionResult> UpdateBlog(int id, [FromForm] CreateBlogDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			int? userId = int.TryParse(userIdClaim, out int parsedId) ? parsedId : null;

			var result = await _blogService.UpdateBlogAsync(id, dto, userId);

			if (result == null)
			{
				return NotFound(new { message = "Không tìm thấy blog cần cập nhật." });
			}

			return Ok(new
			{
				message = "Cập nhật blog thành công.",
				data = result
			});
		}

		[Authorize]
		[HttpDelete("DeleteBlog/{id}")]
		public async Task<IActionResult> DeleteBlog(int id)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			int? userId = int.TryParse(userIdClaim, out int parsedId) ? parsedId : null;

			var success = await _blogService.DeleteBlogAsync(id, userId);

			if (!success)
			{
				return NotFound(new { message = "Không tìm thấy blog để xoá." });
			}

			return Ok(new
			{
				message = "Xoá blog thành công.",
				data = new { blogId = id }
			});
		}

	}
}