using BCrypt.Net;

namespace TripWiseAPI.Utils
{
    public class PasswordHelper
    {
        public static string HashPasswordBCrypt(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
        public static bool VerifyPasswordBCrypt(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
