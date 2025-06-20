using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Text;
using System.Reflection.Metadata;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;
using static System.Reflection.Metadata.BlobBuilder;
namespace TripWiseAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class BlogController : ControllerBase
	{
		private IConfiguration _configuration;
		private readonly TripWiseDBContext _context;

		public BlogController(TripWiseDBContext context, IConfiguration configuration)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		}

		[HttpGet("GetBlogs")]
		public async Task<ActionResult<IEnumerable<BlogDto>>> GetBlogs()
		{
			var blogs = await _context.Blogs
				.Include(b => b.BlogImages)
					.ThenInclude(bi => bi.Image)
				.Where(b => b.RemovedDate == null)
				.Select(b => new BlogDto
				{
					BlogID = b.BlogId,
					BlogName = b.BlogName,
					BlogContent = b.BlogContent,
					CreatedDate = b.CreatedDate,
					CreatedBy = b.CreatedBy,
					UserName = _context.Users
						.Where(u => u.UserId == b.CreatedBy)
						.Select(u => u.UserName)
						.FirstOrDefault(),
					ModifiedDate = b.ModifiedDate,
					ModifiedBy = b.ModifiedBy,
					BlogImages = b.BlogImages.Select(img => new BlogImageDto
					{
						BlogImageID = img.BlogImageId,
						ImageID = img.ImageId,
						ImageURL = img.Image.ImageUrl,
						ImageAlt = img.Image.ImageAlt
					}).ToList()
				})
				.ToListAsync();
			if (blogs == null)
			{
				return NotFound(new { message = "Không có bài blog nào" });
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
			var blogs = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.Where(ba => ba.RemovedDate != null)
				.Select(b => new BlogDto
				{
					BlogID = b.BlogId,
					BlogName = b.BlogName,
					BlogContent = b.BlogContent,
					CreatedDate = b.CreatedDate,
					CreatedBy = b.CreatedBy,
					UserName = _context.Users
						.Where(u => u.UserId == b.RemovedBy)
						.Select(u => u.UserName)
						.FirstOrDefault(),
					RemovedDate = b.ModifiedDate,
					RemovedBy = b.RemovedBy,
					BlogImages = b.BlogImages.Select(img => new BlogImageDto
					{
						BlogImageID = img.BlogImageId,
						ImageID = img.ImageId,
						ImageURL = img.Image.ImageUrl,
						ImageAlt = img.Image.ImageAlt
					}).ToList()
				})
				.ToListAsync();

			if (blogs == null)
			{
				return NotFound(new { message = "Không có bài blog nào" });
			}
			return Ok(new
			{
				message = "Lấy danh sách blog thành công.",
				data = blogs
			});
		}

		[HttpGet("GetBlogById/{id}")]
		public async Task<ActionResult<BlogDto>> GetBlog(int id)
		{
			var blogs = await _context.Blogs
				.Include(b => b.BlogImages).ThenInclude(url => url.Image)
				.Where(b => b.BlogId == id && b.RemovedDate == null)
				.Select(b => new BlogDto
				{
					BlogID = b.BlogId,
					BlogName = b.BlogName,
					BlogContent = b.BlogContent,
					CreatedDate = b.CreatedDate,
					CreatedBy = b.CreatedBy,
					ModifiedDate = b.ModifiedDate,
					ModifiedBy = b.ModifiedBy,
					BlogImages = b.BlogImages.Select(img => new BlogImageDto
					{
						BlogImageID = img.BlogImageId,
						ImageID = img.ImageId,
						ImageURL = img.Image.ImageUrl,
						ImageAlt = img.Image.ImageAlt
					}).ToList()
				}).FirstOrDefaultAsync();
			if (blogs == null)
			{
				return NotFound(new { message = "Không tìm thấy blog ." });
			}
			return Ok(new
			{
				message = "Lấy danh sách blog thành công.",
				data = blogs
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

			var blog = new Blog
			{
				BlogName = dto.BlogName,
				BlogContent = dto.BlogContent,
				CreatedDate = DateTime.Now,
				CreatedBy = userId,
				BlogImages = new List<BlogImage>()
			};

			var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
			if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

			List<Image> addedImages = new();

			// 1. Xử lý ảnh upload từ máy
			if (dto.Images != null)
			{
				foreach (var imageFile in dto.Images)
				{
					if (imageFile != null && imageFile.Length > 0)
					{
						var uniqueFileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
						var filePath = Path.Combine(uploadsFolder, uniqueFileName);

						using var fileStream = new FileStream(filePath, FileMode.Create);
						await imageFile.CopyToAsync(fileStream);

						string relativeUrl = $"/uploads/{uniqueFileName}";

						var image = new Image
						{
							ImageUrl = relativeUrl, // Đảm bảo không bị null
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						};

						_context.Images.Add(image);
						await _context.SaveChangesAsync();

						blog.BlogImages.Add(new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						});

						addedImages.Add(image);
					}
				}
			}

			// 2. Xử lý các URL ảnh từ client
			if (dto.ImageUrls != null)
			{
				foreach (var url in dto.ImageUrls)
				{
					if (!string.IsNullOrWhiteSpace(url))
					{
						var image = new Image
						{
							ImageUrl = url.Trim(), // Đảm bảo không null
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						};

						_context.Images.Add(image);
						await _context.SaveChangesAsync();

						blog.BlogImages.Add(new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						});

						addedImages.Add(image);
					}
				}
			}

			_context.Blogs.Add(blog);
			await _context.SaveChangesAsync();

			var result = new BlogDto
			{
				BlogID = blog.BlogId,
				BlogName = blog.BlogName,
				BlogContent = blog.BlogContent,
				UserName = _context.Users
						.Where(u => u.UserId == blog.CreatedBy)
						.Select(u => u.UserName)
						.FirstOrDefault(),
				BlogImages = blog.BlogImages.Select(bi =>
				{
					var img = addedImages.FirstOrDefault(i => i.ImageId == bi.ImageId);
					return new BlogImageDto
					{
						BlogImageID = bi.BlogImageId,
						ImageID = bi.ImageId,
						ImageURL = img?.ImageUrl
					};
				}).ToList()
			};

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

			var blog = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.FirstOrDefaultAsync(b => b.BlogId == id);

			if (blog == null)
			{
				return NotFound(new { message = "Không tìm thấy blog cần cập nhật." });
			}

			// Cập nhật thông tin blog
			blog.BlogName = dto.BlogName;
			blog.BlogContent = dto.BlogContent;
			blog.ModifiedDate = DateTime.Now;
			blog.ModifiedBy = userId;

			// Xóa các liên kết ảnh cũ
			_context.BlogImages.RemoveRange(blog.BlogImages);
			blog.BlogImages = new List<BlogImage>();

			var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
			if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

			List<Image> addedImages = new();

			// Xử lý ảnh upload
			if (dto.Images != null)
			{
				foreach (var imageFile in dto.Images)
				{
					if (imageFile != null && imageFile.Length > 0)
					{
						var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
						var filePath = Path.Combine(uploadsFolder, uniqueFileName);

						using var fileStream = new FileStream(filePath, FileMode.Create);
						await imageFile.CopyToAsync(fileStream);

						string relativeUrl = $"/uploads/{uniqueFileName}";

						var image = new Image
						{
							ImageUrl = relativeUrl,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						};

						_context.Images.Add(image);
						await _context.SaveChangesAsync();

						var blogImage = new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						};

						blog.BlogImages.Add(blogImage);
						addedImages.Add(image);
					}
				}
			}

			// Xử lý các URL ảnh từ client
			if (dto.ImageUrls != null)
			{
				foreach (var url in dto.ImageUrls)
				{
					if (!string.IsNullOrWhiteSpace(url))
					{
						var image = new Image
						{
							ImageUrl = url.Trim(),
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						};

						_context.Images.Add(image);
						await _context.SaveChangesAsync();

						var blogImage = new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						};

						blog.BlogImages.Add(blogImage);
						addedImages.Add(image);
					}
				}
			}

			await _context.SaveChangesAsync();

			// Tạo DTO kết quả trả về
			var result = new BlogDto
			{
				BlogID = blog.BlogId,
				BlogName = blog.BlogName,
				BlogContent = blog.BlogContent,
				UserName = _context.Users
						.Where(u => u.UserId == blog.ModifiedBy)
						.Select(u => u.UserName)
						.FirstOrDefault(),
				BlogImages = blog.BlogImages.Select(bi =>
				{
					var img = addedImages.FirstOrDefault(i => i.ImageId == bi.ImageId) ?? bi.Image;
					return new BlogImageDto
					{
						BlogImageID = bi.BlogImageId,
						ImageID = bi.ImageId,
						ImageURL = img?.ImageUrl
					};
				}).ToList()
			};

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

			var blog = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.FirstOrDefaultAsync(b => b.BlogId == id);

			if (blog == null)
			{
				return NotFound(new { message = "Không tìm thấy blog để xoá." });
			}

			blog.RemovedDate = DateTime.Now;
			blog.RemovedBy = userId;
			blog.RemovedReason = "Xóa bài blog " + blog.BlogName;

			await _context.SaveChangesAsync();

			return Ok(new
			{
				message = "Xoá blog thành công.",
				data = new { blogId = id }
			});
		}

	}
}



