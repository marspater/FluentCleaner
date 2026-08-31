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
            // we have to inject an exception via reflection if possible,
            // but since it's a static private HttpClient, this is tricky.
            // Alternatively, we can force an exception by removing internet connection
            // or we can test if we can modify the static HttpClient instance.

            // Let's use reflection to replace _http with a mocked one
            var field = typeof(AiExplainer).GetField("_http", BindingFlags.Static | BindingFlags.NonPublic)!;
            var originalHttp = field.GetValue(null);

            var handler = new MockHttpMessageHandler();
            var fakeHttp = new HttpClient(handler);
            field.SetValue(null, fakeHttp);

            // Ensure environment variable is set so it doesn't fail early
            Environment.SetEnvironmentVariable("GROQ_API_KEY", "dummy-key");

            // Also need to clear the cache for this test using reflection
            var cacheField = typeof(AiExplainer).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic)!;
            var cache = (System.Collections.Generic.Dictionary<string, string>)cacheField.GetValue(null)!;
            cache.Clear();

            string result;
            try
            {
                // Act
                result = await AiExplainer.ExplainAsync(entry);
            }
            finally
            {
                // Restore original http client
                field.SetValue(null, originalHttp);
            }

            // Assert
            Assert.Contains("Mock exception", result);
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
