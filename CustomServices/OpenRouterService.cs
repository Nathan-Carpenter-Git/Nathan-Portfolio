using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NathanPortfolio.Models;

namespace NathanPortfolio.CustomServices
{
    // ── Implementation ─────────────────────────────────────────────────────────
    public class OpenRouterService : IOpenRouterService
    {
        private const string Model          = "openrouter/free";
        private const int    MaxContextTokens = 200_000;

        private const int    SafeCharLimit  = (MaxContextTokens - 1_500) * 4;

        private const string OpenRouterUrl  = "https://openrouter.ai/api/v1/chat/completions";
        private const string KeyVaultSecret = "OpenRouterApiKey"; // name of the secret in Key Vault

        private readonly HttpClient    _http;
        private readonly SecretClient  _secretClient;
        private readonly ILogger<OpenRouterService> _logger;

        private string? _cachedApiKey;

        public OpenRouterService (IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OpenRouterService> logger)
        {
            _http   = httpClientFactory.CreateClient("OpenRouter");
            _logger = logger;

            Uri vaultUri = new (configuration.GetSection("VaultURL").Value!);

            _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public async Task<string> SendMessageAsync(List<ChatMessage> history, string systemContext)
        {
            var apiKey = await GetApiKeyAsync();

            var nathanContext = "You are an IT professional named Nathan Carpenter. You have worked in banking for 1-2 years. Please answer as Nathan in a professional setting. Do not answer questions that are outside of the bounds of an IT professional. Here is your resume: " +
                "Systems Administrator with 1-2 years of experience supporting critical banking infrastructure, specializing in Azure, Windows Server, Entra ID, Intune, and enterprise networking. Proven track record of improving uptime, automating infrastructure, and hardening networks in regulated environments." +
                "RELEVANT SKILLS\r\nWindows & Linux Server Administration (WS 2012+, Ubuntu), Networking (Meraki, Cisco), Cloud (Azure, Entra ID, M365, Intune), VoIP systems (Nextiva), Databases (MS SQL Server, MS Access) \r\n" +
                "•\tRELEVANT EXPERIENCE\r\nSystems Administrator | Cattaraugus County Bank | July 2024 – Present\r\n Provided IT support for 60+ staff members across 7 branches as a single point-of-contact\r\n•\tImplemented highly available networking with 99.99% uptime during tenure\r\n•\tManaged virtualized environment for stateless workloads using Azure compute\r\n•\tSupervised DR testing and backup jobs to stay ahead of and prevent future incidents\r\n•\tCreated and configured redundant MS servers for AD DS, AD CS, DHCP, DNS, and NPS\r\n•\tDeployed policies, patches, and applications to 100+ devices using Intune and GPO\r\n•\tUtilized PS scripting to automate Entra onboarding and vital infrastructure notifications\r\n•\tDecreased customer calls to voicemail by 95% introducing call centers and overflow groups\r\n•\tDesigned and deployed an enterprise SQL database to track employee system access,\r\neliminating spreadsheet-based tracking and improving audit readiness.\r\n•\tMinimized lockouts and maximized efficiency with Entra ID SSO and certificate-based auth\r\n" +
                "NOTABLE PROJECTS\r\nWi-Fi Hardening\r\n•\tRemoved insecure internal Wi-Fi and replaced with RADIUS, eliminating shared-password risks\r\n•\tUsed EAP-TLS, pushing certificates via AD CS & group policy\r\n•\tConfigured HA NPS and CRLs for security and reliability\r\n•\tProvided isolated Wi-Fi for employee BYOD via Company Portal & Intune policies\r\nZabbix SNMP System\r\n•\tUsed Hyper-V for Ubuntu container and Docker to containerize Zabbix front and backend\r\n•\tBuilt Zabbix templates to support Meraki Clients & used agents for Windows Servers\r\n•\tCreated SNMP traps to proactively notify admins of server issues or critical client outages\r\n•\tOrganized executive SLA reports through Zabbix for the board of directors\r\n" +
                "EDUCATION\r\nB.S. | Computer Science\r\nEmpire State College \r\nGPA: 3.89\tA.S. | Computer Science\r\nJamestown Community College \r\nGPA: 3.50\r\n\r\n" +
                "CERTIFICATIONS\r\nAZ-104 (in progress), CompTIA Network+ (2026), AZ-900 (2025), AIT (2022), TestOut Network Pro (2021), TestOut PC Pro (2020)\r\n";

            var messages = BuildMessages(history, nathanContext + systemContext);

            var requestBody = new
            {
                model    = Model,
                messages = messages,
                max_tokens = 1_024
            };

            var json    = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl)
            {
                Content = content
            };
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);


            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://nathancarpenter.dev");
            request.Headers.TryAddWithoutValidation("X-Title", "Nathan's Portfolio - TalkToMe");

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
                Role    = "system",
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

            return choices[0]
                   .GetProperty("message")
                   .GetProperty("content")
                   .GetString()
                   ?? string.Empty;
        }
    }
}
