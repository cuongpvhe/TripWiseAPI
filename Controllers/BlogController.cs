using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
	private readonly IBlogService _blogService;

	public BlogController(IBlogService blogService)
	{
		_blogService = blogService;
	}

	[HttpGet("GetBlogs")]
	public async Task<IActionResult> GetBlogs()
	{
		var result = await _blogService.GetBlogsAsync();
		return Ok(new { message = "Lấy danh sách blog thành công", data = result });
	}

	[HttpGet("GetBlogsDeleted")]
	public async Task<IActionResult> GetDeletedBlogs()
	{
		var result = await _blogService.GetDeletedBlogsAsync();
		return Ok(new { message = "Lấy danh sách blog đã xoá thành công", data = result });
	}

	[HttpGet("GetBlogById/{id}")]
	public async Task<IActionResult> GetBlogById(int id)
	{
		var blog = await _blogService.GetBlogByIdAsync(id);
		if (blog == null) return NotFound(new { message = "Không tìm thấy blog." });
		return Ok(new { message = "Thành công", data = blog });
	}

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
