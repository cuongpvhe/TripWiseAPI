using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IPartnerService
    {
        Task<List<PartnerDto>> GetAllAsync();
        Task<List<PartnerDto>> GetAllUserNonActiveAsync();
        Task<bool> CreatePartnerAccountAsync(CreatePartnerAccountDto dto, int createdBy);
        Task<bool> UpdateAsync(int partnerId, PartnerUpdatelDto dto, int modifiedBy);
        Task<PartnerDetailDto?> GetPartnerDetailAsync(int partnerId);
        Task<bool> DeleteAsync(int partnerId, int removedBy, string removedReason);
        Task<bool> SetActiveStatusAsync(int partnerId, int modifiedBy);
        Task<List<ReviewTourDto>> GetTourReviewsByPartnerAsync(int partnerId);
        Task<double> GetAverageRatingByPartnerAsync(int partnerId);
    }
}
