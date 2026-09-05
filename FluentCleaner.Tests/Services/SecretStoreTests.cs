using System;
using System.IO;
using Xunit;
using FluentCleaner.Services;

namespace FluentCleaner.Tests.Services
{
    public class SecretStoreTests
    {
        [Fact]
        public void SaveLoadDeleteSecret_WorksCorrectly()
        {
            var secretName = "UnitTestKey_" + Guid.NewGuid().ToString("N");
            var secretValue = "test-secret-value-12345";

            try
            {
                // Act & Assert Save and Load
                SecretStore.SaveSecret(secretName, secretValue);
                var loaded = SecretStore.LoadSecret(secretName);
                Assert.Equal(secretValue, loaded);

                // Act & Assert Delete
                SecretStore.DeleteSecret(secretName);
                var afterDelete = SecretStore.LoadSecret(secretName);
                Assert.Null(afterDelete);
            }
            finally
            {
                SecretStore.DeleteSecret(secretName);
            }
        }

        [Fact]
        public void SaveSecret_NullOrWhitespace_DeletesSecret()
        {
            var secretName = "UnitTestKey_NullTest_" + Guid.NewGuid().ToString("N");
            var secretValue = "some-key";

            try
            {
                SecretStore.SaveSecret(secretName, secretValue);
                Assert.NotNull(SecretStore.LoadSecret(secretName));

                // Save null should delete
                SecretStore.SaveSecret(secretName, null);
                Assert.Null(SecretStore.LoadSecret(secretName));
            }
            finally
            {
                SecretStore.DeleteSecret(secretName);
            }
        }
    }
}
