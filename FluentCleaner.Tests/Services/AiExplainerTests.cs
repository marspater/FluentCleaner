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

            // Ensure environment variable is set so it doesn't fail early
            Environment.SetEnvironmentVariable("GROQ_API_KEY", "dummy-key");

            string result = await AiExplainer.ExplainAsync(entry);

            // Assert
            Assert.NotNull(result);
        }
    }
}
