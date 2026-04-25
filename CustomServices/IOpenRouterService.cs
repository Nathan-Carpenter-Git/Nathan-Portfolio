using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    public interface IOpenRouterService
    {
        Task<string> SendMessageAsync(List<ChatMessage> history, string systemContext);
    }
}
