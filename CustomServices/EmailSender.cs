using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Net;
using System.Net.Mail;

namespace NathanPortfolio.CustomServices
{
    public class EmailSender : IEmailSender
    {
        private readonly SecretClient _secretClient;

        private string? _cachedSendFromEmail;
        private string? _cachedSendToEmail;
        private string? _cachedEmailPass;

        public EmailSender(IConfiguration configuration)
        {
            Uri keyVaultUri = new(configuration.GetSection("VaultURL").Value!);

            _secretClient = new SecretClient(keyVaultUri, new DefaultAzureCredential());
        }

        public async Task SendEmailAsync(string firstName, string lastName, string fromEmail, string body, IConfiguration configuration)
        {
            var (sendFromEmail, sendToEmail, emailPass) = await GetCredentialsAsync();

            SmtpClient client = new("mail.smtp2go.com", 2525)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(sendFromEmail, emailPass),
            };

            string subject = $"{firstName}, {lastName}, {fromEmail}";

            await client.SendMailAsync(new MailMessage(sendFromEmail, sendToEmail, subject, body));
        }

        /// <summary>
        /// Returns the cached SMTP credentials, fetching them from Key Vault (in parallel) on first call.
        /// </summary>
        private async Task<(string SendFromEmail, string SendToEmail, string EmailPass)> GetCredentialsAsync()
        {
            if (_cachedSendFromEmail is not null && _cachedSendToEmail is not null && _cachedEmailPass is not null)
                return (_cachedSendFromEmail, _cachedSendToEmail, _cachedEmailPass);

            var sendFromEmailTask = _secretClient.GetSecretAsync("Send--From--Email");
            var sendToEmailTask = _secretClient.GetSecretAsync("Send--Email");
            var emailPassTask = _secretClient.GetSecretAsync("Email--Pass");

            await Task.WhenAll(sendFromEmailTask, sendToEmailTask, emailPassTask);

            _cachedSendFromEmail = sendFromEmailTask.Result.Value.Value ?? "";
            _cachedSendToEmail = sendToEmailTask.Result.Value.Value ?? "";
            _cachedEmailPass = emailPassTask.Result.Value.Value ?? "";

            return (_cachedSendFromEmail, _cachedSendToEmail, _cachedEmailPass);
        }
    }
}
