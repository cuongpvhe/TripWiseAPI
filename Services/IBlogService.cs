using TripWiseAPI.Models.DTO;

public interface IBlogService
{
	Task<IEnumerable<BlogDto>> GetBlogsAsync();
	Task<IEnumerable<BlogDto>> GetDeletedBlogsAsync();
	Task<BlogDto> GetBlogByIdAsync(int id);
	Task<BlogDto> CreateBlogAsync(CreateBlogDto dto, int userId);
	Task<BlogDto> UpdateBlogAsync(int id, CreateBlogDto dto, int userId);
	Task<bool> DeleteBlogAsync(int id, int userId);
}
