namespace NathanPortfolio.Models
{
    public class ProjectsViewModel
    {
        public List<GitHubRepo> PinnedRepos { get; set; } = [];
        public bool GitHubAvailable { get; set; }

        public List<ItchGame> RecentGames { get; set; } = [];
        public bool ItchAvailable { get; set; }
    }
}
