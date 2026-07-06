namespace NathanPortfolio.Models
{
    public class GitHubRepo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? LanguageName { get; set; }
        public string? LanguageColor { get; set; }
        public int Stars { get; set; }
        public int Forks { get; set; }
    }
}
