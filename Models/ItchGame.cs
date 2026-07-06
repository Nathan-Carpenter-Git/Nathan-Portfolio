namespace NathanPortfolio.Models
{
    public class ItchGame
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string ShortText { get; set; } = string.Empty;
        public DateTimeOffset? PublishedAt { get; set; }
    }
}
