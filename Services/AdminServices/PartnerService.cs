namespace TripWiseAPI.Services.AdminServices;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Utils;
using System.Security.Cryptography;
using System.Text;
using TripWiseAPI.Models.DTO;
using System.Text.RegularExpressions;

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
        // Validate đầu vào
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email không được để trống");

        if (!Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Email không hợp lệ");

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Mật khẩu không được để trống");

        if (string.IsNullOrWhiteSpace(dto.UserName))
            throw new ArgumentException("Tên người dùng không được để trống");

        if (string.IsNullOrWhiteSpace(dto.CompanyName))
            throw new ArgumentException("Tên công ty không được để trống");

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new ArgumentException("Số điện thoại không được để trống");

        if (!Regex.IsMatch(dto.PhoneNumber, @"^\d{9,15}$"))
            throw new ArgumentException("Số điện thoại không hợp lệ");

        // Kiểm tra trùng email
        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists)
            throw new ArgumentException("Email đã tồn tại trong hệ thống");

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
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email không được để trống");

        if (!Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Email không hợp lệ");

        if (string.IsNullOrWhiteSpace(dto.UserName))
            throw new ArgumentException("Tên người dùng không được để trống");

        if (string.IsNullOrWhiteSpace(dto.CompanyName))
            throw new ArgumentException("Tên công ty không được để trống");

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new ArgumentException("Số điện thoại không được để trống");

        if (!Regex.IsMatch(dto.PhoneNumber, @"^\d{9,11}$"))
            throw new ArgumentException("Số điện thoại không hợp lệ");


        // Kiểm tra email có bị trùng với user khác không
        var emailExists = await _db.Users
    .AnyAsync(u => u.Email == dto.Email && u.Partner != null && u.Partner.PartnerId != partnerId);
        if (emailExists)
            throw new ArgumentException("Email đã tồn tại trong hệ thống");
        // Kiểm tra trùng Số điện thoại
        var phoneExists = await _db.Users
            .AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Partner != null && u.Partner.PartnerId != partnerId);
        if (phoneExists)
            throw new ArgumentException("Số điện thoại đã tồn tại");

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
    public async Task<List<ReviewTourDto>> GetTourReviewsByPartnerAsync(int partnerId)
    {
        // Lấy danh sách review của các tour thường thuộc đối tác cụ thể
        var reviews = await _db.Reviews
            .Where(r => r.RemovedDate == null &&
                        r.Tour.TourTypesId == 2 && // Tour thường
                        r.Tour.PartnerId == partnerId)
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
        var userDict = await _db.Users
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
    public async Task<double> GetAverageRatingByPartnerAsync(int partnerId)
    {
        var avg = await _db.Reviews
            .Where(r => r.RemovedDate == null &&
                        r.Tour.TourTypesId == 2 && // tour thường
                        r.Tour.PartnerId == partnerId)
            .Select(r => (double?)r.Rating)
            .AverageAsync();

        return Math.Round(avg ?? 0, 2); // nếu không có review thì trả về 0
    }

}

