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

    public async Task<bool> CreatePartnerAccountAsync(CreatePartnerAccountDto dto, int createdBy)
    {
        // Kiểm tra trùng email
        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists) return false;

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = PasswordHelper.HashPasswordBCrypt(dto.Password),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
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
            CreatedDate = TimeHelper.GetVietnamTime()
        };

        _db.Partners.Add(partner);
        await _db.SaveChangesAsync();

        return true;
    }
}

