using Microsoft.AspNetCore.Mvc;
using NathanPortfolio.CustomServices;
using NathanPortfolio.Models;

namespace NathanPortfolio.Controllers
{
    public class ProjectsController(
        IGitHubService gitHubService,
        IItchService itchService,
        ILogger<ProjectsController> logger) : Controller
    {
        private readonly IGitHubService _gitHubService = gitHubService;
        private readonly IItchService _itchService = itchService;
        private readonly ILogger<ProjectsController> _logger = logger;

        // Games that live here rather than on itch.io (web apps, not downloadable builds).
        // Shown ahead of the itch.io feed, and shown even if the itch.io API call fails.
        private static readonly List<ItchGame> FeaturedWebGames =
        [
            new ItchGame
            {
                Title = "Headbands",
                Url = "https://headbands.onrender.com",
                CoverUrl = "/shared/games/headbands-cover.svg",
                ShortText = "An online party game where everyone can see the card on your head except you.",
                PublishedAt = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero)
            }
        ];

        public async Task<IActionResult> Index()
        {
            var reposTask = LoadReposAsync();
            var gamesTask = LoadGamesAsync();
            await Task.WhenAll(reposTask, gamesTask);

            var (repos, gitHubAvailable) = reposTask.Result;
            var (games, itchAvailable) = gamesTask.Result;

            var model = new ProjectsViewModel
            {
                PinnedRepos = repos,
                GitHubAvailable = gitHubAvailable,
                RecentGames = games,
                ItchAvailable = itchAvailable
            };

            return View(model);
        }

        private async Task<(List<GitHubRepo>, bool)> LoadReposAsync()
        {
            try
            {
                return (await _gitHubService.GetPinnedReposAsync(), true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load pinned GitHub repos.");
                return ([], false);
            }
        }

        private async Task<(List<ItchGame>, bool)> LoadGamesAsync()
        {
            try
            {
                var itchGames = await _itchService.GetRecentGamesAsync(3);
                return ([.. FeaturedWebGames, .. itchGames], true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recent itch.io games.");
                return (FeaturedWebGames.Count > 0 ? (FeaturedWebGames, true) : ([], false));
            }
        }
    }
}
