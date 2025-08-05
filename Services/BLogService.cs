using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;

public class BlogService : IBlogService
{
	private readonly TripWiseDBContext _context;
	private readonly IWebHostEnvironment _env;
	private readonly IImageUploadService _imageUploadService;

	public BlogService(TripWiseDBContext context, IWebHostEnvironment env, IImageUploadService imageUploadService)
	{
		_context = context;
		_env = env;
		_imageUploadService = imageUploadService;
	}


	public async Task<IEnumerable<BlogDto>> GetBlogsAsync()
	{
		return await _context.Blogs
			.Include(b => b.BlogImages).ThenInclude(bi => bi.Image)
			.Where(b => b.RemovedDate == null)
			.Select(b => new BlogDto
			{
				BlogID = b.BlogId,
				BlogName = b.BlogName,
				BlogContent = b.BlogContent,
				CreatedDate = b.CreatedDate,
				CreatedBy = b.CreatedBy,
				UserName = _context.Users.Where(u => u.UserId == b.CreatedBy).Select(u => u.UserName).FirstOrDefault(),
				ModifiedDate = b.ModifiedDate,
				ModifiedBy = b.ModifiedBy,
				BlogImages = b.BlogImages.Select(img => new BlogImageDto
				{
					BlogImageID = img.BlogImageId,
					ImageID = img.ImageId,
					ImageURL = img.Image.ImageUrl,
					ImageAlt = img.Image.ImageAlt
				}).ToList()
			}).ToListAsync();
	}

	public async Task<IEnumerable<BlogDto>> GetDeletedBlogsAsync()
	{
		return await _context.Blogs
			.Include(b => b.BlogImages).ThenInclude(bi => bi.Image)
			.Where(b => b.RemovedDate != null)
			.Select(b => new BlogDto
			{
				BlogID = b.BlogId,
				BlogName = b.BlogName,
				BlogContent = b.BlogContent,
				CreatedDate = b.CreatedDate,
				CreatedBy = b.CreatedBy,
				UserName = _context.Users.Where(u => u.UserId == b.RemovedBy).Select(u => u.UserName).FirstOrDefault(),
				RemovedDate = b.ModifiedDate,
				RemovedBy = b.RemovedBy,
				BlogImages = b.BlogImages.Select(img => new BlogImageDto
				{
					BlogImageID = img.BlogImageId,
					ImageID = img.ImageId,
					ImageURL = img.Image.ImageUrl,
					ImageAlt = img.Image.ImageAlt
				}).ToList()
			}).ToListAsync();
	}

	public async Task<BlogDto> GetBlogByIdAsync(int id)
	{
		return await _context.Blogs
			.Include(b => b.BlogImages).ThenInclude(bi => bi.Image)
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
	}

	public async Task<BlogDto> CreateBlogAsync(CreateBlogDto dto, int userId)
	{
		var blog = new Blog
		{
			BlogName = dto.BlogName,
			BlogContent = dto.BlogContent,
			CreatedDate = DateTime.Now,
			CreatedBy = userId,			
			BlogImages = new List<BlogImage>()
		};

		List<Image> addedImages = new();

		if (dto.Images != null)
		{
			foreach (var file in dto.Images)
			{
				var imageUrl = await _imageUploadService.UploadImageFromFileAsync(file);
				var image = new Image
				{
					ImageUrl = imageUrl,
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
		if (dto.ImageUrls != null)
		{
			foreach (var url in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
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

		_context.Blogs.Add(blog);
		await _context.SaveChangesAsync();

		return new BlogDto
		{
			BlogID = blog.BlogId,
			BlogName = blog.BlogName,
			BlogContent = blog.BlogContent,
			UserName = _context.Users.Where(u => u.UserId == blog.CreatedBy).Select(u => u.UserName).FirstOrDefault(),
			CreatedBy = blog.CreatedBy,
			CreatedDate = blog.CreatedDate,
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
	}

	public async Task<BlogDto> UpdateBlogAsync(int id, CreateBlogDto dto, int userId)
	{
		var blog = await _context.Blogs.Include(b => b.BlogImages).ThenInclude(bi => bi.Image)
			.FirstOrDefaultAsync(b => b.BlogId == id);

		if (blog == null) return null;

		blog.BlogName = dto.BlogName;
		blog.BlogContent = dto.BlogContent;
		blog.ModifiedDate = DateTime.Now;
		blog.ModifiedBy = userId;

		_context.BlogImages.RemoveRange(blog.BlogImages);
		blog.BlogImages = new List<BlogImage>();

		List<Image> addedImages = new();

		if (dto.Images != null)
		{
			foreach (var file in dto.Images)
			{
				var imageUrl = await _imageUploadService.UploadImageFromFileAsync(file);
				var image = new Image
				{
					ImageUrl = imageUrl,
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


		if (dto.ImageUrls != null)
		{
			foreach (var url in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
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
	

	await _context.SaveChangesAsync();

		return new BlogDto
		{
			BlogID = blog.BlogId,
			BlogName = blog.BlogName,
			BlogContent = blog.BlogContent,
			UserName = _context.Users.Where(u => u.UserId == blog.ModifiedBy).Select(u => u.UserName).FirstOrDefault(),
			CreatedBy = blog.CreatedBy,
			CreatedDate = blog.CreatedDate,
			ModifiedBy = blog.ModifiedBy,
			ModifiedDate = blog.ModifiedDate,
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
	}

	public async Task<bool> DeleteBlogAsync(int id, int userId)
	{
		var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.BlogId == id);
		if (blog == null) return false;

		blog.RemovedDate = DateTime.Now;
		blog.RemovedBy = userId;
		blog.RemovedReason = $"Xoá bài blog {blog.BlogName}";

		await _context.SaveChangesAsync();
		return true;
	}
}
