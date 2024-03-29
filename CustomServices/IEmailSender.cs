namespace NathanPortfolio.CustomServices
{
    public interface IEmailSender
    {
        public Task SendEmailAsync(string firstName, string lastName, string fromEmail, string body, IConfiguration configuration);
    }
}
