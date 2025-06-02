using System.Net;
using System.Net.Mail;

namespace TripWiseAPI.Utils;

public class EmailHelper
{
    static string SenderMail = "phamduycuong2k1@gmail.com";
    static string SenderPassword = "iynk knwu qujv ydwu";

    public static Task SendEmailAsync(string email, string subject, string message)
    {
        var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(SenderMail, SenderPassword)
        };
        var senderAddress = new MailAddress(SenderMail, "Du lịch thông minh");
        var mailMessage = new MailMessage
        {
            From = senderAddress,
            Subject = subject,
            Body = message,
            IsBodyHtml = true
        };

        mailMessage.To.Add(email);

        return client.SendMailAsync(mailMessage);
    }
    
    public static void SendEmailMultiThread(string email, string subject, string body)
    {
        SendEmailAsync(email, subject, body).Wait();
    }
}