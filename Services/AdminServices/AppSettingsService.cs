using Microsoft.EntityFrameworkCore;
using System;
using TripWiseAPI.Models;

namespace TripWiseAPI.Services.AdminServices
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly TripWiseDBContext _dbContext;

        public AppSettingsService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
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


    }


}
