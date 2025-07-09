using TripWiseAPI.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace TripWiseAPI.Utils
{
    public class JwtHelper
    {
        public static string GenerateJwtToken(IConfiguration _configuration, User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var claims = new[] {
                        new Claim(JwtRegisteredClaimNames.Sub, _configuration["Jwt:Subject"]),
                        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                        new Claim(JwtRegisteredClaimNames.Iat, TimeHelper.GetVietnamTime().ToString()),
                        new Claim("UserId", user.UserId.ToString()),
                        new Claim("Username", user.UserName),
                        new Claim(ClaimTypes.Role, user.Role)

             };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: TimeHelper.GetVietnamTime().AddMinutes(30),
                signingCredentials: signIn);
            return tokenHandler.WriteToken(token);
        }

        public static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
    }
}
