using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models.LogModel;
using TripWiseAPI.Utils; // Giữ nguyên nếu bạn có các tiện ích ở đây

namespace TripWiseAPI.Services
{
	public class BlogService : IBlogService
	{
		private readonly TripWiseDBContext _context;
		private readonly IConfiguration _configuration; // Giữ lại nếu bạn cần cấu hình trong service
		private readonly FirebaseLogService _logService; // Giữ lại nếu bạn cần ghi log
		public BlogService(TripWiseDBContext context, IConfiguration configuration, FirebaseLogService logService)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_logService = logService;
		}

		public async Task<IEnumerable<BlogDto>> GetBlogsAsync()
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

			return blogs;
		}

		public async Task<IEnumerable<BlogDto>> GetBlogsDeletedAsync()
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
					// Lưu ý: Trong GetBlogsDeleted, bạn đang lấy UserName của người RemovedBy,
					// trong khi GetBlogs đang lấy UserName của CreatedBy.
					// Hãy đảm bảo đây là hành vi bạn mong muốn.
					UserName = _context.Users
						.Where(u => u.UserId == b.RemovedBy)
						.Select(u => u.UserName)
						.FirstOrDefault(),
					RemovedDate = b.RemovedDate, // Sửa lại thành RemovedDate
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

			return blogs;
		}

		public async Task<BlogDto?> GetBlogByIdAsync(int id)
		{
			var blog = await _context.Blogs
				.Include(b => b.BlogImages).ThenInclude(url => url.Image)
				.Where(b => b.BlogId == id && b.RemovedDate == null)
				.Select(b => new BlogDto
				{
					BlogID = b.BlogId,
					BlogName = b.BlogName,
					BlogContent = b.BlogContent,
					CreatedDate = b.CreatedDate,
					CreatedBy = b.CreatedBy,
					UserName = _context.Users // Thêm UserName cho GetBlogById
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
				}).FirstOrDefaultAsync();

			return blog;
		}

		public async Task<BlogDto> CreateBlogAsync(CreateBlogDto dto, int? userId)
		{
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
							ImageUrl = relativeUrl,
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
							ImageUrl = url.Trim(),
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
			await _logService.LogAsync(userId ?? 0, "Create", $"Tạo blog mới: {dto.BlogName}", 200, createdDate: DateTime.UtcNow, createdBy: userId);
			var result = new BlogDto
			{
				BlogID = blog.BlogId,
				BlogName = blog.BlogName,
				BlogContent = blog.BlogContent,
				UserName = _context.Users
						.Where(u => u.UserId == blog.CreatedBy)
						.Select(u => u.UserName)
						.FirstOrDefault(),
				CreatedDate = blog.CreatedDate, // Đảm bảo trả về CreatedDate
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

			return result;
		}

		public async Task<BlogDto?> UpdateBlogAsync(int id, CreateBlogDto dto, int? userId)
		{
			var blog = await _context.Blogs
				.Include(b => b.BlogImages)
				.ThenInclude(bi => bi.Image)
				.FirstOrDefaultAsync(b => b.BlogId == id);

			if (blog == null)
			{
				return null; // Không tìm thấy blog
			}

			// Cập nhật thông tin blog
			blog.BlogName = dto.BlogName;
			blog.BlogContent = dto.BlogContent;
			blog.ModifiedDate = DateTime.Now;
			blog.ModifiedBy = userId;

			// Xóa các liên kết ảnh cũ
			// Lưu ý: Nếu bạn xóa BlogImage nhưng không xóa Image, các Image không còn liên kết sẽ vẫn còn trong DB.
			// Tùy thuộc vào yêu cầu nghiệp vụ, bạn có thể muốn xóa cả Image nếu nó không được sử dụng ở nơi khác.
			_context.BlogImages.RemoveRange(blog.BlogImages);
			// Không cần gán lại new List<BlogImage>() ở đây vì AddRange sẽ tạo mới
			// blog.BlogImages = new List<BlogImage>();

			var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
			if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

			List<BlogImage> newBlogImages = new List<BlogImage>();
			List<Image> addedImages = new(); // Để theo dõi các ảnh mới được thêm vào DB

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
						await _context.SaveChangesAsync(); // Lưu Image để có ImageId

						newBlogImages.Add(new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						});
						addedImages.Add(image); // Thêm vào danh sách ảnh mới
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
						await _context.SaveChangesAsync(); // Lưu Image để có ImageId

						newBlogImages.Add(new BlogImage
						{
							ImageId = image.ImageId,
							CreatedDate = DateTime.Now,
							CreatedBy = userId
						});
						addedImages.Add(image); // Thêm vào danh sách ảnh mới
					}
				}
			}

			// Cập nhật lại danh sách BlogImages của blog
			blog.BlogImages = newBlogImages;

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
				ModifiedDate = blog.ModifiedDate, // Đảm bảo trả về ModifiedDate
				ModifiedBy = blog.ModifiedBy,
				BlogImages = blog.BlogImages.Select(bi =>
				{
					// Lấy thông tin ảnh từ addedImages (nếu là ảnh mới) hoặc từ bi.Image (nếu nó vẫn là ảnh cũ đã được Include)
					var img = addedImages.FirstOrDefault(i => i.ImageId == bi.ImageId) ?? bi.Image;
					return new BlogImageDto
					{
						BlogImageID = bi.BlogImageId,
						ImageID = bi.ImageId,
						ImageURL = img?.ImageUrl,
						ImageAlt = img?.ImageAlt // Đảm bảo ImageAlt cũng được trả về
					};
				}).ToList()
			};			
			await _logService.LogAsync(userId ?? 0,"Update", $"Cập nhật blog ID {id}: {dto.BlogName}", 200, modifiedDate: DateTime.UtcNow, modifiedBy: userId);
			return result;
		}                   

		public async Task<bool> DeleteBlogAsync(int id, int? userId)
		{
			var blog = await _context.Blogs
				.FirstOrDefaultAsync(b => b.BlogId == id);

			if (blog == null)
			{
				return false; // Không tìm thấy blog
			}

			blog.RemovedDate = DateTime.Now;
			blog.RemovedBy = userId;
			blog.RemovedReason = "Xóa bài blog " + blog.BlogName;
			await _logService.LogAsync(userId ?? 0, "Delete", $"Xóa blog ID {id}", 200, removedDate: DateTime.UtcNow, removedBy: userId);
			await _context.SaveChangesAsync();
			return true;
		}
	}
}