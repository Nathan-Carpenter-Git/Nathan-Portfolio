namespace NathanPortfolio.Models
{
    public class ContactUserMessage
    {
        public Guid Id { get; set; } = new Guid();
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Message { get; set; } = null!;
    }
}
