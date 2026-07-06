using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    public class ItchService : IItchService
    {
        private const string CacheKey = "itch-recent-games";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        private const string KeyVaultSecretName = "ItchApiKey";

        private readonly HttpClient _http;
        private readonly SecretClient _secretClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ItchService> _logger;

        public ItchService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<ItchService> logger)
        {
            _http = httpClientFactory.CreateClient("Itch");
            _cache = cache;
            _logger = logger;

            Uri vaultUri = new(configuration.GetSection("VaultURL").Value!);
            _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
        }

        public async Task<List<ItchGame>> GetRecentGamesAsync(int count = 3)
        {
            if (_cache.TryGetValue(CacheKey, out List<ItchGame>? cached) && cached is not null)
                return cached.Take(count).ToList();

            var apiKey = (await _secretClient.GetSecretAsync(KeyVaultSecretName)).Value.Value;

            var response = await _http.GetAsync($"https://itch.io/api/1/{apiKey}/my-games");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("itch.io API returned {Status}: {Body}", (int)response.StatusCode, errorBody);
                throw new HttpRequestException($"itch.io API error {(int)response.StatusCode}: {errorBody}");
            }

            var games = ParseGames(await response.Content.ReadAsStringAsync());

            _cache.Set(CacheKey, games, CacheDuration);
            return games.Take(count).ToList();
        }

        private static List<ItchGame> ParseGames(string json)
        {
            using var doc = JsonDocument.Parse(json);

            var games = new List<ItchGame>();

            foreach (var node in doc.RootElement.GetProperty("games").EnumerateArray())
            {
                DateTimeOffset? publishedAt = null;
                if (node.TryGetProperty("published_at", out var pub) && pub.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(pub.GetString(), out var parsed))
                {
                    publishedAt = parsed;
                }

                // Skip drafts/unpublished games - they have no published_at.
                if (publishedAt is null)
                    continue;

                games.Add(new ItchGame
                {
                    Title = node.GetProperty("title").GetString() ?? string.Empty,
                    Url = node.GetProperty("url").GetString() ?? string.Empty,
                    CoverUrl = node.TryGetProperty("cover_url", out var cover) ? cover.GetString() : null,
                    ShortText = node.TryGetProperty("short_text", out var text) ? text.GetString() ?? string.Empty : string.Empty,
                    PublishedAt = publishedAt
                });
            }

            return [.. games.OrderByDescending(g => g.PublishedAt)];
        }
    }
}
