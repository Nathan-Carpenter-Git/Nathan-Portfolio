using System.Reflection;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Moq;
using NathanPortfolio.CustomServices;
using Xunit;

namespace NathanPortfolio.Tests
{
    public class EmailSenderCredentialCacheTests
    {
        private static EmailSender CreateEmailSenderWithMockSecretClient(out Mock<SecretClient> secretClientMock)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["VaultURL"] = "https://dummy-vault.vault.azure.net/",
                })
                .Build();

            var emailSender = new EmailSender(configuration);

            secretClientMock = new Mock<SecretClient>();
            secretClientMock
                .Setup(c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, string? _, CancellationToken _) =>
                    Response.FromValue(new KeyVaultSecret(name, $"{name}-value"), Mock.Of<Response>()));

            var secretClientField = typeof(EmailSender).GetField("_secretClient", BindingFlags.NonPublic | BindingFlags.Instance)!;
            secretClientField.SetValue(emailSender, secretClientMock.Object);

            return emailSender;
        }

        private static Task<(string SendFromEmail, string SendToEmail, string EmailPass)> InvokeGetCredentialsAsync(EmailSender emailSender)
        {
            var method = typeof(EmailSender).GetMethod("GetCredentialsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Task<(string, string, string)>)method.Invoke(emailSender, null)!;
        }

        private static void SetCacheExpiresAtUtc(EmailSender emailSender, DateTime value)
        {
            var field = typeof(EmailSender).GetField("_cacheExpiresAtUtc", BindingFlags.NonPublic | BindingFlags.Instance)!;
            field.SetValue(emailSender, value);
        }

        [Fact]
        public async Task GetCredentialsAsync_SecondCallWithinTtl_ReusesCachedSecretsWithoutRefetching()
        {
            var emailSender = CreateEmailSenderWithMockSecretClient(out var secretClientMock);

            var first = await InvokeGetCredentialsAsync(emailSender);
            var second = await InvokeGetCredentialsAsync(emailSender);

            Assert.Equal(first, second);
            secretClientMock.Verify(
                c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task GetCredentialsAsync_AfterTtlExpires_RefetchesSecretsFromKeyVault()
        {
            var emailSender = CreateEmailSenderWithMockSecretClient(out var secretClientMock);

            await InvokeGetCredentialsAsync(emailSender);
            SetCacheExpiresAtUtc(emailSender, DateTime.UtcNow.AddMinutes(-1));
            await InvokeGetCredentialsAsync(emailSender);

            secretClientMock.Verify(
                c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(6));
        }

        [Fact]
        public async Task GetCredentialsAsync_ConcurrentCallsOnColdCache_OnlyFetchSecretsOnce()
        {
            var emailSender = CreateEmailSenderWithMockSecretClient(out var secretClientMock);

            var callTasks = Enumerable.Range(0, 10).Select(_ => InvokeGetCredentialsAsync(emailSender));
            var results = await Task.WhenAll(callTasks);

            Assert.All(results, r => Assert.Equal(results[0], r));
            secretClientMock.Verify(
                c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }
    }
}
