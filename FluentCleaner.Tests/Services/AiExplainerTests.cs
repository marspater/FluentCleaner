using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentCleaner.Services;
using FluentCleaner.Models;

namespace FluentCleaner.Tests.Services
{
    public class AiExplainerTests
    {
        [Fact]
        public async Task ExplainAsync_CatchesException_AndReturnsNetworkErrorMessage()
        {
            // Arrange
            var entry = new CleanerEntry { Name = "TestEntry" };

            // Because AiExplainer uses a static HttpClient (_http),
            // we have to inject an exception via reflection if possible.
            var field = typeof(AiExplainer).GetField("_http", BindingFlags.Static | BindingFlags.NonPublic);
            var originalHttp = field?.GetValue(null);

            var handler = new MockHttpMessageHandler();
            var fakeHttp = new HttpClient(handler);
            field?.SetValue(null, fakeHttp);

            // Ensure environment variable is set so it doesn't fail early
            Environment.SetEnvironmentVariable("GROQ_API_KEY", "dummy-key");

            // Also need to clear the cache for this test using reflection
            var cacheField = typeof(AiExplainer).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
            var cache = cacheField?.GetValue(null) as System.Collections.Generic.Dictionary<string, string>;
            cache?.Clear();

            string result;
            try
            {
                // Act
                result = await AiExplainer.ExplainAsync(entry);
            }
            finally
            {
                // Restore original http client
                field?.SetValue(null, originalHttp);
            }

            // Assert
            Assert.True(result.Contains("Mock exception") || result.Contains("AI_NetworkError"));
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new Exception("Mock exception from HttpClient");
            }
        }
    }
}
