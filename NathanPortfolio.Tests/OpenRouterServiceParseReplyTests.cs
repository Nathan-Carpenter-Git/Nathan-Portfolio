using System.Reflection;
using NathanPortfolio.CustomServices;
using Xunit;

namespace NathanPortfolio.Tests
{
    public class OpenRouterServiceParseReplyTests
    {
        private static string InvokeParseReply(string content)
        {
            var method = typeof(OpenRouterService).GetMethod("ParseReply", BindingFlags.NonPublic | BindingFlags.Static)!;
            var encodedContent = System.Text.Json.JsonSerializer.Serialize(content);
            var json = "{\"choices\":[{\"message\":{\"content\":" + encodedContent + "}}]}";
            return (string)method.Invoke(null, new object[] { json })!;
        }

        [Theory]
        [InlineData("secure — scalable", "secure, scalable")]
        [InlineData("secure—scalable", "secure, scalable")]
        [InlineData("secure – scalable", "secure, scalable")]
        [InlineData("secure&mdash;scalable", "secure, scalable")]
        [InlineData("secure&ndash;scalable", "secure, scalable")]
        [InlineData("banking infrastructure - from multi-site networks", "banking infrastructure, from multi-site networks")]
        public void ParseReply_ReplacesAnyDashVariantUsedAsAPause(string modelOutput, string expected)
        {
            Assert.Equal(expected, InvokeParseReply(modelOutput));
        }

        [Fact]
        public void ParseReply_LeavesHyphenatedCompoundWordsIntact()
        {
            Assert.Equal("multi-site, well-known", InvokeParseReply("multi-site, well-known"));
        }
    }
}
