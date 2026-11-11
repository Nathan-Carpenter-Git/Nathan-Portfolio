using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Text.Json.Serialization;

namespace NathanPortfolio.CustomServices
{
    public class TurnstileValidator : ITurnstileValidator
    {
        public const string HttpClientName = "Turnstile";
        public const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        // Key Vault secret name, used unless "Turnstile:SecretKey" is set in config
        // (Development sets it to Cloudflare's public always-pass test secret).
        private const string KeyVaultSecret = "Turnstile--Secret--Key";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        // Cloudflare rejects tokens longer than this, so don't bother sending them.
        private const int MaxTokenLength = 2048;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TurnstileValidator> _logger;
        private readonly string? _configuredSecret;
        private readonly Lazy<SecretClient> _secretClient;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        private string? _cachedSecret;
        private DateTime _cacheExpiresAtUtc = DateTime.MinValue;

        public TurnstileValidator(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TurnstileValidator> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            var configured = configuration["Turnstile:SecretKey"];
            _configuredSecret = string.IsNullOrWhiteSpace(configured) ? null : configured;

            _secretClient = new Lazy<SecretClient>(() =>
                new SecretClient(new Uri(configuration["VaultURL"]!), new DefaultAzureCredential()));
        }

        public async Task<bool> ValidateAsync(string? token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
                return false;

            try
            {
                var secret = await GetSecretAsync();

                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secret,
                    ["response"] = token,
                });

                var http = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await http.PostAsync(SiteVerifyUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken);
                if (result?.Success == true)
                    return true;

                _logger.LogInformation(
                    "Turnstile rejected a token: {ErrorCodes}",
                    string.Join(", ", result?.ErrorCodes ?? []));
                return false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Turnstile verification failed; rejecting the submission.");
                return false;
            }
        }

        private async Task<string> GetSecretAsync()
        {
            if (_configuredSecret is not null)
                return _configuredSecret;

            if (_cachedSecret is not null && DateTime.UtcNow < _cacheExpiresAtUtc)
                return _cachedSecret;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedSecret is not null && DateTime.UtcNow < _cacheExpiresAtUtc)
                    return _cachedSecret;

                var secret = await _secretClient.Value.GetSecretAsync(KeyVaultSecret);
                _cachedSecret = secret.Value.Value
                    ?? throw new InvalidOperationException($"Key Vault secret '{KeyVaultSecret}' is empty.");
                _cacheExpiresAtUtc = DateTime.UtcNow.Add(CacheDuration);

                return _cachedSecret;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private sealed class SiteVerifyResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
