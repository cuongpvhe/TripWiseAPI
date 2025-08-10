using TripWiseAPI.Models;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IAppSettingsService
    {
        Task<List<AppSetting>> GetAllAsync();
        Task<AppSetting> GetByKeyAsync(string key);
        Task<bool> UpdateAsync(AppSetting dto);

        Task<int> GetIntValueAsync(string key, int defaultValue = 0);
        Task<bool> SetFreePlanAsync(string planName);
        Task<bool> SetTrialPlanAsync(string planName);
        Task<bool> SetValueAsync(string key, string value);
        Task UpdateTimeoutAsync(int minutes);
        Task<int> GetTimeoutAsync();
        Task<int?> GetOtpTimeoutAsync();
        Task<bool> UpdateOtpTimeoutAsync(int timeoutSeconds);

        Task UpdateValueAsync(int id, string value);
    }
}
