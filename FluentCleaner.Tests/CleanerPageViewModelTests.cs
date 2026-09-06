using FluentCleaner.Models;
using FluentCleaner.ViewModels;
using System.Diagnostics;
using Xunit;

namespace FluentCleaner.Tests;

public class CleanerPageViewModelTests
{
    [Fact]
    public async Task CleanCategoryAsync_CleansSelectedEntriesCorrectly()
    {
        var vm = new CleanerPageViewModel();
        var category = new CleanerCategoryViewModel("Test Category");

        var entry1 = new CleanerEntry { Name = "App 1", Default = true };
        var entry2 = new CleanerEntry { Name = "App 2", Default = true };
        var entry3 = new CleanerEntry { Name = "App 3", Default = false };

        var vm1 = new CleanerEntryViewModel(entry1) { IsSelected = true };
        var vm2 = new CleanerEntryViewModel(entry2) { IsSelected = true };
        var vm3 = new CleanerEntryViewModel(entry3) { IsSelected = false };

        category.Entries.Add(vm1);
        category.Entries.Add(vm2);
        category.Entries.Add(vm3);

        vm.Categories.Add(category);

        await vm.CleanCategoryAsync(category);

        Assert.False(vm.IsBusy);
        Assert.Contains("App 1", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanCategoryAsync_PerformanceBenchmark()
    {
        var vm = new CleanerPageViewModel();
        var category = new CleanerCategoryViewModel("Benchmark Category");

        // Add 1000 selected entries to category
        int count = 1000;
        for (int i = 0; i < count; i++)
        {
            var entry = new CleanerEntry { Name = $"BenchApp_{i}", Default = true };
            var entryVm = new CleanerEntryViewModel(entry) { IsSelected = true };
            category.Entries.Add(entryVm);
        }

        vm.Categories.Add(category);

        var sw = Stopwatch.StartNew();
        await vm.CleanCategoryAsync(category);
        sw.Stop();

        // Ensure execution completed without throwing
        Assert.False(vm.IsBusy);
    }
}
