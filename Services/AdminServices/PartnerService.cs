namespace TripWiseAPI.Services.AdminServices;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Utils;
using System.Security.Cryptography;
using System.Text;
using TripWiseAPI.Models.DTO;

public class PartnerService : IPartnerService
{
    private readonly TripWiseDBContext _db;

    public PartnerService(TripWiseDBContext db)
    {
        _db = db;
    }
    public async Task<List<PartnerDto>> GetAllAsync()
    {
        return await _db.Partners
            .Include(p => p.User)
            .Where(p =>
            p.User.Role == "PARTNER" && 
            p.RemovedDate == null)
            .Select(p => new PartnerDto
            {
                PartnerId = p.PartnerId,
                UserId = p.UserId,
                Email = p.User.Email,
                CompanyName = p.CompanyName,
                PhoneNumber = p.PhoneNumber,
                Address = p.Address,
                Website = p.Website,
                IsActive = p.User.IsActive,
                CreatedDate = p.CreatedDate
            }).ToListAsync();
    }
    public async Task<List<PartnerDto>> GetAllUserNonActiveAsync()
    {
        return await _db.Partners
            .Include(p => p.User)
            .Where(p =>
            p.User.Role == "PARTNER" &&
            p.RemovedDate != null)
            .Select(p => new PartnerDto
            {
                PartnerId = p.PartnerId,
                UserId = p.UserId,
                Email = p.User.Email,
                CompanyName = p.CompanyName,
                PhoneNumber = p.PhoneNumber,
                Address = p.Address,
                Website = p.Website,
                IsActive = p.User.IsActive,
                CreatedDate = p.CreatedDate,
                RemovedDate = p.RemovedDate
            }).ToListAsync();
    }
    public async Task<bool> CreatePartnerAccountAsync(CreatePartnerAccountDto dto, int createdBy)
    {
        // Kiểm tra trùng email
        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists) return false;

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = PasswordHelper.HashPasswordBCrypt(dto.Password),
            UserName = dto.UserName,
            Role = "PARTNER",
            IsActive = true,
            CreatedDate = TimeHelper.GetVietnamTime(),
            CreatedBy = createdBy
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var partner = new Partner
        {
            UserId = user.UserId,
            CompanyName = dto.CompanyName,
            PhoneNumber = dto.PhoneNumber,
            Address = dto.Address,
            Website = dto.Website,
            IsActive = true,
            CreatedDate = TimeHelper.GetVietnamTime(),
            CreatedBy = createdBy
        };

        _db.Partners.Add(partner);
        await _db.SaveChangesAsync();

        return true;
    }
    public async Task<bool> UpdateAsync(int partnerId, PartnerUpdatelDto dto, int modifiedBy)
    {
        var partner = await _db.Partners
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PartnerId == partnerId && p.RemovedDate == null);

        if (partner == null || partner.User == null)
            return false;

        // update user
        partner.User.UserName = dto.UserName;
        partner.User.Email = dto.Email;
        partner.User.ModifiedDate = TimeHelper.GetVietnamTime();
        partner.User.ModifiedBy = modifiedBy;

        // update partner
        partner.CompanyName = dto.CompanyName;
        partner.PhoneNumber = dto.PhoneNumber;
        partner.Address = dto.Address;
        partner.Website = dto.Website;
        partner.ModifiedDate = TimeHelper.GetVietnamTime();
        partner.ModifiedBy = modifiedBy;

        await _db.SaveChangesAsync();
        return true;
    }
    public async Task<PartnerDetailDto?> GetPartnerDetailAsync(int partnerId)
    {
        var partner = await _db.Partners
            .Include(p => p.User)
            .FirstOrDefaultAsync(p =>
                p.PartnerId == partnerId &&
                p.User != null &&
                p.User.Role == "PARTNER");

        if (partner == null)
            return null;
        async Task<string?> GetUserNameById(int? id)
        {
            if (!id.HasValue) return null;
            return await _db.Users
                .Where(u => u.UserId == id)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync();
        }
        var dto = new PartnerDetailDto
        {
            PartnerId = partner.PartnerId,
            UserId = partner.UserId,
            Email = partner.User.Email,
            UserName = partner.User.UserName,
            PhoneNumber = partner.PhoneNumber,
            CompanyName = partner.CompanyName,
            Address = partner.Address,
            Website = partner.Website,
            IsActive = partner.User.IsActive,
            CreatedDate = partner.CreatedDate,
            CreatedBy = partner.CreatedBy,
            ModifiedDate = partner.ModifiedDate,
            ModifiedBy = partner.ModifiedBy,
            CreatedByName = await GetUserNameById(partner.User.CreatedBy),
            ModifiedByName = await GetUserNameById(partner.User.CreatedBy)
        };
        if (!partner.User.IsActive)
        {
            dto.RemovedBy = partner.User.RemovedBy;
            dto.RemovedByName = await GetUserNameById(partner.User.RemovedBy);
            dto.RemovedReason = partner.User.RemovedReason;
            dto.RemovedDate = partner.User.RemovedDate;
        }
        return dto;
    }

    public async Task<bool> DeleteAsync(int partnerId, int removedBy, string removedReason)
    {
        var partner = await _db.Partners
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PartnerId == partnerId && p.User.RemovedDate == null);

        if (partner == null || partner.User == null)
            return false;

        partner.IsActive = false;
        partner.RemovedDate = TimeHelper.GetVietnamTime();
        partner.RemovedBy = removedBy;
        partner.RemovedReason = removedReason;

        partner.User.IsActive = false;
        partner.User.RemovedDate = TimeHelper.GetVietnamTime();
        partner.User.RemovedBy = removedBy;
        partner.User.RemovedReason = removedReason;

        await _db.SaveChangesAsync();
        return true;
    }
    public async Task<bool> SetActiveStatusAsync(int partnerId, int modifiedBy)
    {
        var partner = await _db.Partners
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PartnerId == partnerId);

        if (partner == null || partner.User == null)
            return false;

        // Cập nhật trạng thái ở bảng Partner
        partner.IsActive = true;
        partner.ModifiedDate = TimeHelper.GetVietnamTime();
        partner.ModifiedBy = modifiedBy;
        partner.RemovedDate = null;
        partner.RemovedBy = null;
        partner.RemovedReason = null;

        // Cập nhật trạng thái ở bảng User
        partner.User.IsActive = true;
        partner.User.ModifiedDate = TimeHelper.GetVietnamTime();
        partner.User.ModifiedBy = modifiedBy;
        partner.User.RemovedDate = null;
        partner.User.RemovedBy = null;
        partner.User.RemovedReason = null;

        await _db.SaveChangesAsync();
        return true;
    }

}

