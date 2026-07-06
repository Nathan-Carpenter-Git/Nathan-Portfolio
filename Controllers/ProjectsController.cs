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
                return (await _itchService.GetRecentGamesAsync(3), true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load recent itch.io games.");
                return ([], false);
            }
        }
    }
}
