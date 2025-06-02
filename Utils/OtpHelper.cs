namespace TripWiseAPI.Utils;

public class OtpHelper
{
    public static string GenerateRandomDigits(int length)
    {
        Random random = new Random();
        string result = string.Empty;
        
        for (int i = 0; i < length; i++)
        {
            result += random.Next(0, 10).ToString();
        }
        
        return result;
    }
}