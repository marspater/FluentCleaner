using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentCleaner.ViewModels;

namespace FluentCleaner.Tests.ViewModels;

public class DeveloperCleanupViewModelTests
{
    [Fact]
    public async Task ScanAsync_FindsTargetDirectories_And_NukeAsync_RemovesThem()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        var nodeModulesDir = Path.Combine(tempFolder, "node_modules");
        var binDir = Path.Combine(tempFolder, "bin");
        var normalDir = Path.Combine(tempFolder, "src");

        Directory.CreateDirectory(tempFolder);
        Directory.CreateDirectory(nodeModulesDir);
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(normalDir);

        File.WriteAllText(Path.Combine(nodeModulesDir, "test.js"), "console.log('test');");
        File.WriteAllText(Path.Combine(binDir, "app.dll"), "dll");

        try
        {
            var vm = new DeveloperCleanupViewModel
            {
                RootPath = tempFolder,
                ScanNodeModules = true,
                ScanBinObj = true,
                ScanTarget = false,
                ScanBuildDist = false
            };

            await vm.ScanCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.TrashDirectories.Count);
            Assert.True(vm.HasResults);

            await vm.NukeCommand.ExecuteAsync(null);

            Assert.False(Directory.Exists(nodeModulesDir));
            Assert.False(Directory.Exists(binDir));
            Assert.True(Directory.Exists(normalDir));
            Assert.Equal(0, vm.TrashDirectories.Count);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task NukeAsync_HandlesReadOnlyFiles_Successfully()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "FluentCleanerTest_" + Guid.NewGuid().ToString("N"));
        var targetDir = Path.Combine(tempFolder, "target");
        Directory.CreateDirectory(targetDir);

        var readOnlyFile = Path.Combine(targetDir, "readonly.txt");
        File.WriteAllText(readOnlyFile, "read only content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        try
        {
            var vm = new DeveloperCleanupViewModel
            {
                RootPath = tempFolder,
                ScanNodeModules = false,
                ScanBinObj = false,
                ScanTarget = true,
                ScanBuildDist = false
            };

            await vm.ScanCommand.ExecuteAsync(null);
            Assert.Single(vm.TrashDirectories);

            await vm.NukeCommand.ExecuteAsync(null);

            Assert.False(Directory.Exists(targetDir));
            Assert.Equal(0, vm.TrashDirectories.Count);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(tempFolder, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                    }
                    Directory.Delete(tempFolder, true);
                }
                catch { }
            }
        }
    }

    [Fact]
    public async Task ScanAsync_RejectsInvalidOrUnsafePath()
    {
        var vm = new DeveloperCleanupViewModel
        {
            RootPath = "C:\\"
        };

        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Contains("Cannot scan drive roots or protected system directories", vm.StatusText);
        Assert.Empty(vm.TrashDirectories);
    }
}
