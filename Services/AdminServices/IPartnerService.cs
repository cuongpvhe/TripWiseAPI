using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IPartnerService
    {
        Task<bool> CreatePartnerAccountAsync(CreatePartnerAccountDto dto, int createdBy);
    }
}
