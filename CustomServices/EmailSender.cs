using System.Net;
using System.Net.Mail;

namespace NathanPortfolio.CustomServices
{
    public class EmailSender : IEmailSender
    {
        public async Task SendEmailAsync(string firstName, string lastName, string fromEmail, string body, IConfiguration configuration)
        {
            string sendFromEmail = configuration["email:send-from-email"] ?? "";
            string sendToEmail = configuration["email:send-to-email"] ?? "";
            string emailPass = configuration["passwords:email-password"] ?? "";

            var client = new SmtpClient("smtp.office365.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(sendFromEmail, emailPass),
            };

            string subject = $"{firstName}, {lastName}, {fromEmail}";

            await client.SendMailAsync(new MailMessage(sendFromEmail, sendToEmail, subject, body));
        }
    }
}
