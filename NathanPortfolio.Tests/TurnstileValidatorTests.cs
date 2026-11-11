using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NathanPortfolio.CustomServices;

namespace NathanPortfolio.Tests
{
    public class TurnstileValidatorTests
    {
        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add((request, body));
                return respond(request);
            }
        }

        private static TurnstileValidator CreateValidator(StubHandler handler, string secret = "test-secret")
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["VaultURL"] = "https://dummy-vault.vault.azure.net/",
                    ["Turnstile:SecretKey"] = secret,
                })
                .Build();

            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(TurnstileValidator.HttpClientName)).Returns(() => new HttpClient(handler));

            return new TurnstileValidator(factory.Object, configuration, NullLogger<TurnstileValidator>.Instance);
        }

        private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

        [Fact]
        public async Task ValidateAsync_CloudflareSaysSuccess_ReturnsTrueAndPostsSecretAndToken()
        {
            var handler = new StubHandler(_ => Json("""{"success":true,"error-codes":[]}"""));
            var validator = CreateValidator(handler);

            Assert.True(await validator.ValidateAsync("abc.token"));

            var (request, body) = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(TurnstileValidator.SiteVerifyUrl, request.RequestUri!.ToString());
            Assert.Contains("secret=test-secret", body);
            Assert.Contains("response=abc.token", body);
        }

        [Fact]
        public async Task ValidateAsync_CloudflareSaysFailure_ReturnsFalse()
        {
            var handler = new StubHandler(_ => Json("""{"success":false,"error-codes":["invalid-input-response"]}"""));

            Assert.False(await CreateValidator(handler).ValidateAsync("bad.token"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ValidateAsync_MissingToken_ReturnsFalseWithoutCallingCloudflare(string? token)
        {
            var handler = new StubHandler(_ => Json("""{"success":true}"""));

            Assert.False(await CreateValidator(handler).ValidateAsync(token));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task ValidateAsync_OversizedToken_ReturnsFalseWithoutCallingCloudflare()
        {
            var handler = new StubHandler(_ => Json("""{"success":true}"""));

            Assert.False(await CreateValidator(handler).ValidateAsync(new string('a', 2049)));
            Assert.Empty(handler.Requests);
        }

        [Fact]
        public async Task ValidateAsync_CloudflareServerError_FailsClosed()
        {
            var handler = new StubHandler(_ => Json("""{"success":true}""", HttpStatusCode.InternalServerError));

            Assert.False(await CreateValidator(handler).ValidateAsync("abc.token"));
        }

        [Fact]
        public async Task ValidateAsync_NetworkError_FailsClosed()
        {
            var handler = new StubHandler(_ => throw new HttpRequestException("network down"));

            Assert.False(await CreateValidator(handler).ValidateAsync("abc.token"));
        }
    }
}
