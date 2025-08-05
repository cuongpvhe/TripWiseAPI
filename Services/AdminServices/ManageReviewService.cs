using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services.AdminServices
{
    public class ManageReviewService : IManageReviewService
    {
        private readonly TripWiseDBContext _dbContext;
        public ManageReviewService( TripWiseDBContext dbContext)
        {
            
            _dbContext = dbContext;
        }
        public async Task<double> GetAverageRatingOfAiToursAsync()
        {
            var ratings = await _dbContext.Reviews
                .Where(r => r.Tour.TourTypesId == 1) 
                .Select(r => r.Rating)
                .ToListAsync(); 

            var average = ratings.DefaultIfEmpty(0).Average(); 
            return Math.Round((double)average, 2);
        }

        public async Task<List<ReviewResponseDto>> GetAiTourReviewsAsync()
        {
            // Lấy danh sách review tour AI
            var reviews = await _dbContext.Reviews
                .Where(r => r.RemovedDate == null && r.Tour.TourTypesId == 1)
                .Select(r => new
                {
                    r.ReviewId,
                    r.Rating,
                    r.Comment,
                    r.CreatedBy,
                    r.CreatedDate
                })
                .ToListAsync();

            // Lấy danh sách userId duy nhất
            var createdByIds = reviews
                .Select(r => r.CreatedBy)
                .Where(id => id.HasValue)
                .Distinct()
                .ToList();

            // Lấy user name
            var userDict = await _dbContext.Users
                .Where(u => createdByIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.UserName);

            // Map dữ liệu
            var result = reviews.Select(r => new ReviewResponseDto
            {
                ReviewId = r.ReviewId,
                Rating = (int)r.Rating,
                Comment = r.Comment,
                CreatedBy = r.CreatedBy,
                CreatedDate = r.CreatedDate,
                UserName = r.CreatedBy.HasValue && userDict.ContainsKey(r.CreatedBy.Value)
                    ? userDict[r.CreatedBy.Value]
                    : null
            }).ToList();

            return result;
        }
        public async Task<List<ReviewTourDto>> GetTourReviewsAsync()
        {
            // Lấy danh sách review tour thường
            var reviews = await _dbContext.Reviews
                .Where(r => r.RemovedDate == null && r.Tour.TourTypesId == 2)
                .Select(r => new
                {
                    r.ReviewId,
                    r.TourId,
                    r.Rating,
                    r.Comment,
                    r.CreatedBy,
                    r.CreatedDate,
                    PartnerId = r.Tour.PartnerId,
                    PartnerName = r.Tour.Partner.CompanyName
                })
                .ToListAsync();

            // Lấy danh sách userId duy nhất
            var createdByIds = reviews
                .Select(r => r.CreatedBy)
                .Where(id => id.HasValue)
                .Distinct()
                .ToList();

            // Lấy user name
            var userDict = await _dbContext.Users
                .Where(u => createdByIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.UserName);

            // Map dữ liệu
            var result = reviews.Select(r => new ReviewTourDto
            {
                ReviewId = r.ReviewId,
                TourId = r.TourId,
                Rating = (int)r.Rating,
                Comment = r.Comment,
                CreatedBy = r.CreatedBy,
                CreatedDate = r.CreatedDate,
                UserName = r.CreatedBy.HasValue && userDict.ContainsKey(r.CreatedBy.Value)
                    ? userDict[r.CreatedBy.Value]
                    : null,
                PartnerId = r.PartnerId,
                PartnerName = r.PartnerName
            }).ToList();

            return result;
        }

        public async Task<bool> DeleteReviewAsync(int reviewId, int removedBy, string reason)
        {
            var review = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.RemovedDate == null);
            if (review == null) return false;

            review.RemovedDate = TimeHelper.GetVietnamTime();
            review.RemovedBy = removedBy;
            review.RemovedReason = reason;

            await _dbContext.SaveChangesAsync();
            return true;
        }

    }
}
