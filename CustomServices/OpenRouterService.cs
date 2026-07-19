using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    // ── Implementation ─────────────────────────────────────────────────────────
    public class OpenRouterService : IOpenRouterService
    {
        private const string Model = "openrouter/free";
        private const int MaxContextTokens = 200_000;

        private const int SafeCharLimit = (MaxContextTokens - 1_500) * 4;

        private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
        private const string KeyVaultSecret = "OpenRouterApiKey"; // name of the secret in Key Vault

        private readonly HttpClient _http;
        private readonly SecretClient _secretClient;
        private readonly ILogger<OpenRouterService> _logger;

        private string? _cachedApiKey;

        public OpenRouterService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OpenRouterService> logger)
        {
            _http = httpClientFactory.CreateClient("OpenRouter");
            _logger = logger;

            Uri vaultUri = new(configuration.GetSection("VaultURL").Value!);

            _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public async Task<string> SendMessageAsync(List<ChatMessage> history, string systemContext)
        {
            var apiKey = await GetApiKeyAsync();

            var messages = BuildMessages(history, systemContext);

            var requestBody = new
            {
                model = Model,
                messages = messages,
                max_tokens = 2_048
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl)
            {
                Content = content
            };
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);


            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://nathansporfolio.azurewebsites.net");
            request.Headers.TryAddWithoutValidation("X-Title", "Nathan's Portfolio");

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenRouter returned {Status}: {Body}",
                    (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"OpenRouter API error {(int)response.StatusCode}: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return ParseReply(responseJson);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the cached API key, fetching it from Key Vault on first call.
        /// </summary>
        private async Task<string> GetApiKeyAsync()
        {
            if (_cachedApiKey is not null)
                return _cachedApiKey;

            _logger.LogInformation("Fetching OpenRouter API key from Azure Key Vault…");
            var secret = await _secretClient.GetSecretAsync(KeyVaultSecret);
            _cachedApiKey = secret.Value.Value;
            return _cachedApiKey;
        }

        /// <summary>
        /// Prepends the system prompt and trims the oldest messages if the
        /// estimated character count would exceed the model's context window.
        /// </summary>
        private static List<ChatMessage> BuildMessages(
            List<ChatMessage> history,
            string systemContext)
        {
            var systemMsg = new ChatMessage
            {
                Role = "system",
                Content = string.IsNullOrWhiteSpace(systemContext)
                            ? "You are a helpful assistant."
                            : systemContext.Trim()
            };

            var trimmed = new List<ChatMessage>(history);

            while (trimmed.Count > 0)
            {
                int totalChars = systemMsg.Content.Length
                    + trimmed.Sum(m => m.Content.Length);

                if (totalChars <= SafeCharLimit)
                    break;

                if (trimmed.Count >= 2)
                    trimmed.RemoveRange(0, 2);
                else
                    trimmed.RemoveAt(0);
            }

            var messages = new List<ChatMessage> { systemMsg };
            messages.AddRange(trimmed);
            return messages;
        }

        /// <summary>
        /// Pulls the assistant's text out of the OpenRouter JSON response.
        /// </summary>
        private static string ParseReply(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var choices = doc.RootElement
                             .GetProperty("choices");

            if (choices.GetArrayLength() == 0)
                throw new InvalidOperationException("OpenRouter returned no choices.");

            var content = choices[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString()
                   ?? string.Empty;

            // The system prompt asks the model not to use dashes, but LLM output isn't
            // guaranteed to follow stylistic instructions, enforce it deterministically so
            // replies always match the site's dash-free style. Normalize surrounding
            // whitespace too, since models often omit spaces around em/en dashes. A bare
            // hyphen is only treated as a pause dash (not a hyphenated compound word) when
            // it has whitespace on both sides.
            return Regex.Replace(content, @"\s*(—|–|&mdash;|&ndash;)\s*|\s+-\s+", ", ");
        }
    }
}
