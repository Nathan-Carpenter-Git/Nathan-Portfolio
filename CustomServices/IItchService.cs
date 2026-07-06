using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    public interface IItchService
    {
        Task<List<ItchGame>> GetRecentGamesAsync(int count = 3);
    }
}
