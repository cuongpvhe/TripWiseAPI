using Microsoft.EntityFrameworkCore;
using System;
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

        public async Task<List<AppSetting>> GetAllAsync()
        {
            return await _dbContext.AppSettings
                .Select(x => new AppSetting
                {
                    Id = x.Id,
                    Key = x.Key,
                    Value = x.Value
                })
                .ToListAsync();
        }

        public async Task<AppSetting> GetByKeyAsync(string key)
        {
            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (setting == null) return null;

            return new AppSetting
            {
                Id = setting.Id,
                Key = setting.Key,
                Value = setting.Value
            };
        }

        public async Task<bool> UpdateAsync(AppSetting dto)
        {
            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == dto.Key);
            if (setting == null) return false;

            setting.Value = dto.Value;
            _dbContext.AppSettings.Update(setting);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<string?> GetValueAsync(string key)
        {
            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
            return setting?.Value;
        }
        public async Task<int> GetIntValueAsync(string key, int defaultValue = 0)
        {
            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (setting == null) return defaultValue;

            return int.TryParse(setting.Value, out int val) ? val : defaultValue;
        }
        public async Task<bool> SetFreePlanAsync(string planName)
        {
            // Kiểm tra Plan có tồn tại
            var exists = await _dbContext.Plans
                .AnyAsync(p => p.PlanName == planName && p.RemovedDate == null);

            if (!exists)
                return false;

            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == "FreePlan");
            if (setting == null)
            {
                setting = new AppSetting
                {
                    Key = "FreePlan",
                    Value = planName
                };
                await _dbContext.AppSettings.AddAsync(setting);
            }
            else
            {
                setting.Value = planName;
                _dbContext.AppSettings.Update(setting);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetTrialPlanAsync(string planName)
        {
            // Kiểm tra Plan có tồn tại
            var exists = await _dbContext.Plans
                .AnyAsync(p => p.PlanName == planName && p.RemovedDate == null);

            if (!exists)
                return false;

            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == "DefaultTrialPlanName");
            if (setting == null)
            {
                setting = new AppSetting
                {
                    Key = "DefaultTrialPlanName",
                    Value = planName
                };
                await _dbContext.AppSettings.AddAsync(setting);
            }
            else
            {
                setting.Value = planName;
                _dbContext.AppSettings.Update(setting);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<bool> SetValueAsync(string key, string value)
        {
            var setting = await _dbContext.AppSettings.FirstOrDefaultAsync(x => x.Key == key);

            if (setting == null)
            {
                setting = new AppSetting { Key = key, Value = value };
                await _dbContext.AppSettings.AddAsync(setting);
            }
            else
            {
                setting.Value = value;
                _dbContext.AppSettings.Update(setting);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetTimeoutAsync()
        {
            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "SessionTimeoutMinutes");

            return setting != null ? int.Parse(setting.Value) : 0;
        }

        public async Task UpdateTimeoutAsync(int minutes)
        {
            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "SessionTimeoutMinutes");

            if (setting != null)
            {
                setting.Value = minutes.ToString();
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                _dbContext.AppSettings.Add(new AppSetting
                {
                    Key = "SessionTimeoutMinutes",
                    Value = minutes.ToString()
                });
                await _dbContext.SaveChangesAsync();
            }
        }
        public async Task<int?> GetOtpTimeoutAsync()
        {
            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(x => x.Key == "OTP_TIMEOUT");

            if (setting == null)
                return null;

            if (int.TryParse(setting.Value, out int minutes))
                return minutes;

            return null;
        }

        public async Task<bool> UpdateOtpTimeoutAsync(int timeoutMinutes)
        {

            if (timeoutMinutes <= 0)
                return false;

            var setting = await _dbContext.AppSettings
                .FirstOrDefaultAsync(x => x.Key == "OTP_TIMEOUT");

            if (setting == null)
            {
                setting = new AppSetting
                {
                    Key = "OTP_TIMEOUT",
                    Value = timeoutMinutes.ToString()
                };
                _dbContext.AppSettings.Add(setting);
            }
            else
            {
                setting.Value = timeoutMinutes.ToString();
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }



        public async Task<List<HotNewsDto>> GetAllHotNewAsync()
        {
            var data = await _dbContext.AppSettings
                .Where(x => x.Key.StartsWith("HotNews_"))
                .ToListAsync();

            return data.Select(x =>
            {
                var json = JsonSerializer.Deserialize<Dictionary<string, string>>(x.Value);
                return new HotNewsDto
                {
                    Id = x.Id,
                    ImageUrl = json.ContainsKey("ImageUrl") ? json["ImageUrl"] : null,
                    RedirectUrl = json.ContainsKey("RedirectUrl") ? json["RedirectUrl"] : null
                };
            }).ToList();
        }


        public async Task<HotNewsDto?> GetByIdAsync(int id)
        {
            var setting = await _dbContext.AppSettings.FindAsync(id);
            if (setting == null) return null;

            var dto = JsonSerializer.Deserialize<HotNewsDto>(setting.Value);
            dto.Id = setting.Id;
            return dto;
        }

        public async Task<int> CreateAsync(HotNewsRequest request)
        {
            // --- Upload ảnh ---
            string? uploadedUrl = null;
            if (request.ImageFile != null)
                uploadedUrl = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
            else if (!string.IsNullOrEmpty(request.ImageUrl))
                uploadedUrl = await _imageUploadService.UploadImageFromUrlAsync(request.ImageUrl);

            if (string.IsNullOrEmpty(uploadedUrl))
                throw new Exception("Ảnh không hợp lệ!");

            var dto = new HotNewsDto
            {
                ImageUrl = uploadedUrl,
                RedirectUrl = request.RedirectUrl
            };

            var setting = new AppSetting
            {
                Key = $"HotNews_{Guid.NewGuid()}",
                Value = JsonSerializer.Serialize(dto)
            };

            _dbContext.AppSettings.Add(setting);
            await _dbContext.SaveChangesAsync();
            return setting.Id;
        }

        public async Task<bool> UpdateAsync(int id, HotNewsRequest request)
        {
            var setting = await _dbContext.AppSettings.FindAsync(id);
            if (setting == null) return false;

            var dto = JsonSerializer.Deserialize<HotNewsDto>(setting.Value);

            // --- Upload ảnh mới nếu có ---
            if (request.ImageFile != null)
                dto.ImageUrl = await _imageUploadService.UploadImageFromFileAsync(request.ImageFile);
            else if (!string.IsNullOrEmpty(request.ImageUrl))
                dto.ImageUrl = await _imageUploadService.UploadImageFromUrlAsync(request.ImageUrl);

            // Cập nhật RedirectUrl nếu truyền vào
            if (!string.IsNullOrEmpty(request.RedirectUrl))
                dto.RedirectUrl = request.RedirectUrl;

            setting.Value = JsonSerializer.Serialize(dto);

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var setting = await _dbContext.AppSettings.FindAsync(id);
            if (setting == null) return false;

            // Xoá ảnh trên Cloudinary nếu cần
            var dto = JsonSerializer.Deserialize<HotNewsDto>(setting.Value);
            if (dto != null && !string.IsNullOrEmpty(dto.ImageUrl))
            {
                var publicId = _imageUploadService.GetPublicIdFromUrl(dto.ImageUrl);
                if (!string.IsNullOrEmpty(publicId))
                    await _imageUploadService.DeleteImageAsync(publicId);
            }

            _dbContext.AppSettings.Remove(setting);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }


}
