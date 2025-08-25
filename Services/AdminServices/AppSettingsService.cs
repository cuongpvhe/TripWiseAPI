using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services.AdminServices
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly TripWiseDBContext _dbContext;
        private readonly IImageUploadService _imageUploadService;

        public AppSettingsService(TripWiseDBContext dbContext, IImageUploadService imageUploadService)
        {
            _dbContext = dbContext;
            _imageUploadService = imageUploadService;
        }

        /// <summary>
        /// Lấy cấu hình theo key (internal).
        /// </summary>
        private async Task<AppSetting?> GetByKeyInternalAsync(string key) =>
            await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == key);

        /// <summary>
        /// Tạo mới hoặc cập nhật cấu hình theo key.
        /// </summary>
        /// <param name="key">Tên khóa cấu hình</param>
        /// <param name="value">Giá trị cần lưu</param>
        private async Task<bool> SetOrUpdateValueAsync(string key, string value)
        {
            var setting = await GetByKeyInternalAsync(key);
            if (setting == null)
                await _dbContext.AppSettings.AddAsync(new AppSetting { Key = key, Value = value });
            else
                setting.Value = value;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Lấy tất cả AppSettings.
        /// </summary>
        public async Task<List<AppSetting>> GetAllAsync() =>
            await _dbContext.AppSettings
                .Select(x => new AppSetting { Id = x.Id, Key = x.Key, Value = x.Value })
                .ToListAsync();

        /// <summary>
        /// Lấy cấu hình theo key.
        /// </summary>
        public async Task<AppSetting?> GetByKeyAsync(string key) =>
            await GetByKeyInternalAsync(key);

        /// <summary>
        /// Cập nhật cấu hình từ DTO.
        /// </summary>
        public async Task<bool> UpdateAsync(AppSetting dto) =>
            await SetOrUpdateValueAsync(dto.Key, dto.Value);

        /// <summary>
        /// Lấy giá trị cấu hình theo key (string).
        /// </summary>
        public async Task<string?> GetValueAsync(string key) =>
            (await GetByKeyInternalAsync(key))?.Value;

        /// <summary>
        /// Lấy giá trị cấu hình dạng số nguyên.
        /// </summary>
        /// <param name="key">Tên khóa</param>
        /// <param name="defaultValue">Giá trị mặc định nếu không tìm thấy hoặc sai định dạng</param>
        public async Task<int> GetIntValueAsync(string key, int defaultValue = 0)
        {
            var value = await GetValueAsync(key);
            return int.TryParse(value, out var val) ? val : defaultValue;
        }

        /// <summary>
        /// Cập nhật gói FreePlan mặc định.
        /// </summary>
        public async Task<bool> SetFreePlanAsync(string planName)
        {
            var exists = await _dbContext.Plans
                .AnyAsync(p => p.PlanName == planName && p.RemovedDate == null);
            return exists && await SetOrUpdateValueAsync("FreePlan", planName);
        }

        /// <summary>
        /// Cập nhật gói TrialPlan mặc định.
        /// </summary>
        public async Task<bool> SetTrialPlanAsync(string planName)
        {
            var exists = await _dbContext.Plans
                .AnyAsync(p => p.PlanName == planName && p.RemovedDate == null);
            return exists && await SetOrUpdateValueAsync("DefaultTrialPlanName", planName);
        }

        /// <summary>
        /// Lưu giá trị AppSetting.
        /// </summary>
        public async Task<bool> SetValueAsync(string key, string value) =>
            await SetOrUpdateValueAsync(key, value);

        /// <summary>
        /// Lấy timeout session (phút).
        /// </summary>
        public async Task<int> GetTimeoutAsync() =>
            await GetIntValueAsync("SessionTimeoutMinutes");

        /// <summary>
        /// Cập nhật timeout session.
        /// </summary>
        public async Task UpdateTimeoutAsync(int minutes) =>
            await SetOrUpdateValueAsync("SessionTimeoutMinutes", minutes.ToString());

        /// <summary>
        /// Lấy thời gian timeout OTP (phút).
        /// </summary>
        public async Task<int?> GetOtpTimeoutAsync()
        {
            var value = await GetValueAsync("OTP_TIMEOUT");
            return int.TryParse(value, out var minutes) ? minutes : null;
        }

        /// <summary>
        /// Cập nhật thời gian timeout OTP.
        /// </summary>
        public async Task<bool> UpdateOtpTimeoutAsync(int timeoutMinutes) =>
            timeoutMinutes > 0 && await SetOrUpdateValueAsync("OTP_TIMEOUT", timeoutMinutes.ToString());

        /// <summary>
        /// Lấy danh sách HotNews.
        /// </summary>
        public async Task<List<HotNewsDto>> GetAllHotNewAsync()
        {
            var data = await _dbContext.AppSettings
                .Where(x => x.Key.StartsWith("HotNews_"))
                .ToListAsync();

            return data.Select(x =>
            {
                var json = JsonSerializer.Deserialize<HotNewsJson>(x.Value);

                return new HotNewsDto
                {
                    Id = x.Id,
                    ImageUrl = json?.ImageUrl,
                    RedirectUrl = json?.RedirectUrl,
                    CreatedDate = x.CreatedDate

                };
            }).ToList();
        }

        /// <summary>
        /// Lấy HotNews theo Id.
        /// </summary>
        public async Task<HotNewsDto?> GetByIdAsync(int id)
        {
            var setting = await _dbContext.AppSettings.FindAsync(id);
            if (setting == null) return null;

            var dto = JsonSerializer.Deserialize<HotNewsDto>(setting.Value) ?? new HotNewsDto();


            dto.Id = setting.Id;
            dto.CreatedBy = setting.CreatedBy;
            dto.CreatedDate = setting.CreatedDate;
            dto.ModifiedBy = setting.ModifiedBy;
            dto.ModifiedDate = setting.ModifiedDate;
            dto.RemovedBy = setting.RemovedBy;
            dto.RemovedDate = setting.RemovedDate;
            dto.RemovedReason = setting.RemovedReason;

            return dto;
        }

        /// <summary>
        /// Tạo mới HotNews.
        /// </summary>
        /// <param name="request">Request tạo HotNews</param>
        /// <param name="createdBy">Người tạo</param>
        /// <returns>Id của HotNews vừa tạo</returns>
        public async Task<int> CreateAsync(HotNewsRequest request, string createdBy)
        {
            var uploadedUrl = request.ImageFile != null
                ? await _imageUploadService.UploadImageFromFileAsync(request.ImageFile)
                : !string.IsNullOrEmpty(request.ImageUrl)
                    ? await _imageUploadService.UploadImageFromUrlAsync(request.ImageUrl)
                    : null;

            if (string.IsNullOrEmpty(uploadedUrl))
                throw new Exception("Ảnh không hợp lệ!");

            var dto = new HotNewsDto { ImageUrl = uploadedUrl, RedirectUrl = request.RedirectUrl };

            var setting = new AppSetting
            {
                Key = $"HotNews_{Guid.NewGuid()}",
                Value = JsonSerializer.Serialize(new { dto.ImageUrl, dto.RedirectUrl }),
                CreatedBy = createdBy,
                CreatedDate = TimeHelper.GetVietnamTime()
            };

            _dbContext.AppSettings.Add(setting);
            await _dbContext.SaveChangesAsync();

            return setting.Id;
        }

        /// <summary>
        /// Cập nhật HotNews.
        /// </summary>
        /// <param name="id">Id HotNews</param>
        /// <param name="request">Dữ liệu cập nhật</param>
        /// <param name="modifiedBy">Người sửa</param>
        public async Task<bool> UpdateAsync(int id, HotNewsRequest request, string modifiedBy)
        {
            var setting = await _dbContext.AppSettings.FindAsync(id);
            if (setting == null) return false;

            // Deserialize giá trị hiện tại
            var dto = JsonSerializer.Deserialize<HotNewsDto>(setting.Value) ?? new HotNewsDto();

            // Cập nhật hình ảnh nếu có
            if (request.ImageFile != null)
                dto.ImageUrl = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
            else if (!string.IsNullOrEmpty(request.ImageUrl))
                dto.ImageUrl = await _imageUploadService.UploadImageFromUrlAsync(request.ImageUrl);

            // Cập nhật RedirectUrl nếu có
            if (!string.IsNullOrEmpty(request.RedirectUrl))
                dto.RedirectUrl = request.RedirectUrl;

            // Cập nhật audit trên entity, nhưng không lưu vào JSON
            setting.ModifiedDate = TimeHelper.GetVietnamTime();
            setting.ModifiedBy = modifiedBy;

            // Chỉ serialize các trường cần thiết
            var valueToStore = new { dto.ImageUrl, dto.RedirectUrl };
            setting.Value = JsonSerializer.Serialize(valueToStore);

            await _dbContext.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Xóa HotNews.
        /// </summary>
        /// <param name="id">Id HotNews</param>
        public async Task<bool> DeleteAsync(int id)
        {
            var setting = await _dbContext.AppSettings.FindAsync(id);
            if (setting == null) return false;

            _dbContext.AppSettings.Remove(setting);
            await _dbContext.SaveChangesAsync();
            return true;
        }


    }
}
