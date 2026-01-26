using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Net;
using System.Net.Mail;

namespace NathanPortfolio.CustomServices
{
    public class EmailSender : IEmailSender
    {
        public async Task SendEmailAsync(string firstName, string lastName, string fromEmail, string body, IConfiguration configuration)
        {
            Uri keyVaultUri = new(configuration.GetSection("VaultURL").Value!);

            var secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());

            KeyVaultSecret sendFromEmailSecret = await secretClient.GetSecretAsync("Send--From--Email");
            KeyVaultSecret sendToEmailSecret = await secretClient.GetSecretAsync("Send--Email");
            KeyVaultSecret emailPassSecret = await secretClient.GetSecretAsync("Email--Pass");

            string sendFromEmail = sendFromEmailSecret.Value ?? "";
            string sendToEmail = sendToEmailSecret.Value ?? "";
            string emailPass = emailPassSecret.Value ?? "";

            SmtpClient client = new("mail.smtp2go.com", 2525)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(sendFromEmail, emailPass),
            };

            string subject = $"{firstName}, {lastName}, {fromEmail}";

            await client.SendMailAsync(new MailMessage(sendFromEmail, sendToEmail, subject, body));
        }
    }
}
