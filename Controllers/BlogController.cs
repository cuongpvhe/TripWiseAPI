using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing.Text;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;
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
		[HttpGet]
		public async Task<ActionResult<IEnumerable<BlogDto>>> GetBlogs()
		{
			var blogs = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.Select(b => new BlogDto
				{
					BlogID = b.BlogId,
					BlogName = b.BlogName,
					BlogContent = b.BlogContent,
					BlogImages = b.BlogImages.Select(img => new BlogImageDto
					{
						BlogImageID = img.BlogImageId,
						ImageID = img.ImageId,
						ImageURL = img.Image.ImageUrl
					}).ToList()
				})
				.ToListAsync();

			return Ok(new
			{
				success = true,
				message = "Lấy danh sách blog thành công.",
				data = blogs
			});
		}


		[HttpGet("{id}")]
		public async Task<ActionResult<BlogDto>> GetBlog(int id)
		{
			var blog = await _context.Blogs
				.Include(b => b.BlogImages).ThenInclude(url => url.Image)
				.Where(b => b.BlogId == id)
				.Select(b => new BlogDto
				{

					BlogID = b.BlogId,
					BlogName = b.BlogName,
					BlogContent = b.BlogContent,
					BlogImages = b.BlogImages.Select(img => new BlogImageDto
					{
						BlogImageID = img.BlogImageId,
						ImageID = img.ImageId,
						ImageURL = img.Image.ImageUrl
					}).ToList()
				}).FirstOrDefaultAsync();

			if (blog == null) return NotFound();

			return Ok(new
			{
				success = true,
				message = "Lấy danh sách blog thành công.",
				data = blog
			});
		}
		[HttpPost]
		public async Task<IActionResult> CreateBlog([FromForm] CreateBlogDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			int? userId = int.TryParse(userIdClaim, out int parsedId) ? parsedId : null;

			var blog = new Blog
			{
				BlogName = dto.BlogName,
				BlogContent = dto.BlogContent,
				CreatedDate = DateTime.Now,
				CreatedBy = null,
				BlogImages = new List<BlogImage>()
			};

			// Tạo thư mục nếu chưa có
			var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
			if (!Directory.Exists(uploadsFolder))
			{
				Directory.CreateDirectory(uploadsFolder);
			}

			// ✅ 1. Xử lý ảnh upload từ máy
			if (dto.Images != null && dto.Images.Count > 0)
			{
				foreach (var imageFile in dto.Images)
				{
					if (imageFile.Length > 0)
					{
						var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
						var filePath = Path.Combine(uploadsFolder, uniqueFileName);

						using (var fileStream = new FileStream(filePath, FileMode.Create))
						{
							await imageFile.CopyToAsync(fileStream);
						}

						string relativeUrl = $"/uploads/{uniqueFileName}";

						var image = new Image
						{
							ImageUrl = relativeUrl,
							CreatedDate = DateTime.Now,
							CreatedBy = null
						};

						_context.Images.Add(image);
						await _context.SaveChangesAsync();

						blog.BlogImages.Add(new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = null
						});
					}
				}
			}

			// ✅ 2. Xử lý ảnh từ URL
			if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
			{
				foreach (var url in dto.ImageUrls)
				{
					var image = new Image
					{
						ImageUrl = url,
						CreatedDate = DateTime.Now,
						CreatedBy = null
					};

					_context.Images.Add(image);
					await _context.SaveChangesAsync();

					blog.BlogImages.Add(new BlogImage
					{
						ImageId = image.ImageId,
						CreatedDate = DateTime.Now,
						CreatedBy = null
					});
				}
			}

			_context.Blogs.Add(blog);
			await _context.SaveChangesAsync();

			// Trả về thông tin blog sau khi tạo
			var result = new BlogDto
			{
				BlogID = blog.BlogId,
				BlogName = blog.BlogName,
				BlogContent = blog.BlogContent,
				BlogImages = blog.BlogImages.Select(bi => new BlogImageDto
				{
					BlogImageID = bi.BlogImageId,
					ImageID = bi.ImageId,
					ImageURL = _context.Images.FirstOrDefault(i => i.ImageId == bi.ImageId)?.ImageUrl
				}).ToList()
			};

			return Ok(new
			{
				success = true,
				message = "Tạo blog thành công.",
				data = result
			});
		}


		[HttpPut("{id}")]
		public async Task<IActionResult> UpdateBlog(int id, [FromForm] CreateBlogDto dto)
		{
			var blog = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.FirstOrDefaultAsync(b => b.BlogId == id);

			if (blog == null)
			{
				return NotFound(new
				{
					success = false,
					message = "Không tìm thấy blog cần cập nhật."
				});
			}

			blog.BlogName = dto.BlogName;
			blog.BlogContent = dto.BlogContent;
			blog.ModifiedDate = DateTime.Now;
			blog.ModifiedBy = null;

			// Xoá các liên kết ảnh cũ
			_context.BlogImages.RemoveRange(blog.BlogImages);
			blog.BlogImages = new List<BlogImage>();

			// ✅ 1. Xử lý ảnh tải từ máy
			var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
			if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

			if (dto.Images != null && dto.Images.Count > 0)
			{
				foreach (var imageFile in dto.Images)
				{
					if (imageFile.Length > 0)
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
							CreatedBy = null
						};

						_context.Images.Add(image);
						await _context.SaveChangesAsync();

						blog.BlogImages.Add(new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = null
						});
					}
				}
			}

			// ✅ 2. Xử lý ảnh từ URL
			if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
			{
				foreach (var url in dto.ImageUrls)
				{
					var image = new Image
					{
						ImageUrl = url,
						CreatedDate = DateTime.Now,
						CreatedBy = null
					};

					_context.Images.Add(image);
					await _context.SaveChangesAsync();

					blog.BlogImages.Add(new BlogImage
					{
						ImageId = image.ImageId,
						CreatedDate = DateTime.Now,
						CreatedBy = null
					});
				}
			}

			await _context.SaveChangesAsync();

			// ✅ Trả về dữ liệu blog sau khi cập nhật
			var result = new BlogDto
			{
				BlogID = blog.BlogId,
				BlogName = blog.BlogName,
				BlogContent = blog.BlogContent,
				BlogImages = blog.BlogImages.Select(bi => new BlogImageDto
				{
					BlogImageID = bi.BlogImageId,
					ImageID = bi.ImageId,
					ImageURL = bi.Image?.ImageUrl
				}).ToList()
			};

			return Ok(new
			{
				success = true,
				message = "Cập nhật blog thành công.",
				data = result
			});
		}

		// DELETE: api/blog/5
		[HttpDelete("{id}")]
		public async Task<IActionResult> DeleteBlog(int id)
		{
			var blog = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.FirstOrDefaultAsync(b => b.BlogId == id);

			if (blog == null)
			{
				return NotFound(new
				{
					success = false,
					message = "Không tìm thấy blog để xoá."
				});
			}

			// Lưu lại danh sách ảnh cũ
			var oldImages = blog.BlogImages.Select(bi => bi.Image).ToList();

			// Xoá các liên kết ảnh cũ
			_context.BlogImages.RemoveRange(blog.BlogImages);
			blog.BlogImages = new List<BlogImage>();

			// ⚠️ Gọi SaveChanges trước khi xóa Image để tránh lỗi FK
			await _context.SaveChangesAsync();

			// Xoá ảnh vật lý và bản ghi ảnh nếu không còn dùng
			foreach (var image in oldImages)
			{
				var isUsedElsewhere = await _context.BlogImages.AnyAsync(bi => bi.ImageId == image.ImageId);
				if (!isUsedElsewhere)
				{
					_context.Images.Remove(image);

					// Xoá ảnh vật lý nếu là ảnh upload
					if (!string.IsNullOrEmpty(image.ImageUrl) && image.ImageUrl.StartsWith("/uploads/"))
					{
						var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.ImageUrl.TrimStart('/'));
						if (System.IO.File.Exists(filePath))
						{
							System.IO.File.Delete(filePath);
						}
					}
				}
			}

			// Xoá chính blog
			_context.Blogs.Remove(blog);
			await _context.SaveChangesAsync();

			return Ok(new
			{
				success = true,
				message = "Xoá blog thành công.",
				data = new { blogId = id }
			});
		}
	}
	}



