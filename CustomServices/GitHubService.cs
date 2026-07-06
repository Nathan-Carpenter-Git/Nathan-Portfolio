using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    public class GitHubService : IGitHubService
    {
        private const string CacheKey = "github-pinned-repos";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        private const string KeyVaultSecretName = "GitHubToken";
        private const string GraphQlUrl = "https://api.github.com/graphql";

        private readonly HttpClient _http;
        private readonly SecretClient _secretClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GitHubService> _logger;

        public GitHubService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<GitHubService> logger)
        {
            _http = httpClientFactory.CreateClient("GitHub");
            _cache = cache;
            _configuration = configuration;
            _logger = logger;

            Uri vaultUri = new(configuration.GetSection("VaultURL").Value!);
            _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
        }

        public async Task<List<GitHubRepo>> GetPinnedReposAsync()
        {
            if (_cache.TryGetValue(CacheKey, out List<GitHubRepo>? cached) && cached is not null)
                return cached;

            var username = _configuration.GetSection("GitHubUsername").Value
                ?? throw new InvalidOperationException("GitHubUsername is not configured.");

            var token = (await _secretClient.GetSecretAsync(KeyVaultSecretName)).Value.Value;

            const string query = """
                query($login: String!) {
                  user(login: $login) {
                    pinnedItems(first: 6, types: REPOSITORY) {
                      nodes {
                        ... on Repository {
                          name
                          description
                          url
                          stargazerCount
                          forkCount
                          primaryLanguage { name color }
                        }
                      }
                    }
                  }
                }
                """;

            var requestBody = new
            {
                query,
                variables = new { login = username }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NathanPortfolio", "1.0"));

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("GitHub GraphQL returned {Status}: {Body}", (int)response.StatusCode, errorBody);
                throw new HttpRequestException($"GitHub API error {(int)response.StatusCode}: {errorBody}");
            }

            var repos = ParseRepos(await response.Content.ReadAsStringAsync());

            _cache.Set(CacheKey, repos, CacheDuration);
            return repos;
        }

        private static List<GitHubRepo> ParseRepos(string json)
        {
            using var doc = JsonDocument.Parse(json);

            var nodes = doc.RootElement
                .GetProperty("data")
                .GetProperty("user")
                .GetProperty("pinnedItems")
                .GetProperty("nodes");

            var repos = new List<GitHubRepo>();

            foreach (var node in nodes.EnumerateArray())
            {
                var repo = new GitHubRepo
                {
                    Name = node.GetProperty("name").GetString() ?? string.Empty,
                    Description = node.GetProperty("description").GetString() ?? string.Empty,
                    Url = node.GetProperty("url").GetString() ?? string.Empty,
                    Stars = node.GetProperty("stargazerCount").GetInt32(),
                    Forks = node.GetProperty("forkCount").GetInt32()
                };

                if (node.TryGetProperty("primaryLanguage", out var lang) && lang.ValueKind == JsonValueKind.Object)
                {
                    repo.LanguageName = lang.GetProperty("name").GetString();
                    repo.LanguageColor = lang.GetProperty("color").GetString();
                }

                repos.Add(repo);
            }

            return repos;
        }
    }
}
