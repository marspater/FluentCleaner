using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentCleaner.ViewModels;
using Xunit;

namespace FluentCleaner.Tests.ViewModels;

public class DeveloperCleanupViewModelTests
{
    [Fact]
    public async Task ScanAsync_FindsTargetDirectories()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "FC_DevCleanupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var targetSubDir = Path.Combine(tempDir, "node_modules");
        Directory.CreateDirectory(targetSubDir);

        try
        {
            var vm = new DeveloperCleanupViewModel
            {
                RootPath = tempDir,
                ScanNodeModules = true
            };

            // Act
            await vm.ScanCommand.ExecuteAsync(null);

            // Assert
            Assert.True(vm.HasResults);
            Assert.Single(vm.TrashDirectories);
            Assert.Equal(targetSubDir, vm.TrashDirectories[0].Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ScanAsync_WhenCancelled_SetsCancelledStatus()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "FC_DevCleanupTest_Cancel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        for (int i = 0; i < 5; i++)
        {
            var sub = Path.Combine(tempDir, $"Folder_{i}");
            Directory.CreateDirectory(sub);
            Directory.CreateDirectory(Path.Combine(sub, "node_modules"));
        }

        try
        {
            var vm = new DeveloperCleanupViewModel
            {
                RootPath = tempDir,
                ScanNodeModules = true
            };

            var scanTask = vm.ScanCommand.ExecuteAsync(null);
            vm.CancelCommand.Execute(null);

            await scanTask;

            Assert.Equal("Scan cancelled.", vm.StatusText);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
