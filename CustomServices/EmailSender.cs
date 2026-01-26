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

            string sendFromEmail = sendFromEmailSecret.Value ?? "awdacawsawdwad@nathan-carpenter.org";
            string sendToEmail = sendToEmailSecret.Value ?? "carpenternathan6@gmail.com";
            string emailPass = emailPassSecret.Value ?? "S45MxCrryxJ21XNe";

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
