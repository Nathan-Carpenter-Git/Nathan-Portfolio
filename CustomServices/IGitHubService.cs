using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    public interface IGitHubService
    {
        Task<List<GitHubRepo>> GetPinnedReposAsync();
    }
}
