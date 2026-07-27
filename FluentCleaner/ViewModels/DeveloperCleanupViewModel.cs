using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentCleaner.Models;

namespace FluentCleaner.ViewModels;

public partial class TrashDirectoryViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsSelected { get; set; } = true;
    [ObservableProperty] public partial string SizeText { get; set; } = "Calculating...";
    
    public string Path { get; }
    public string FolderName => System.IO.Path.GetFileName(Path);
    public string ParentPath => System.IO.Path.GetDirectoryName(Path) ?? "";
    public long SizeBytes { get; set; }
    
    public TrashDirectoryViewModel(string path)
    {
        Path = path;
    }
}

public partial class DeveloperCleanupViewModel : ObservableObject
{
    [ObservableProperty] public partial string RootPath { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Select a directory to scan for developer workspace trash.";
    [ObservableProperty] public partial bool HasResults { get; set; }
    [ObservableProperty] public partial bool SelectAll { get; set; } = true;

    // Scan target folders options
    [ObservableProperty] public partial bool ScanNodeModules { get; set; } = true;
    [ObservableProperty] public partial bool ScanBinObj { get; set; } = true;
    [ObservableProperty] public partial bool ScanTarget { get; set; } = true;
    [ObservableProperty] public partial bool ScanBuildDist { get; set; }

    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<TrashDirectoryViewModel> TrashDirectories { get; } = new();

    private CancellationTokenSource? _cts;
    private bool _isUpdatingSelection;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        ScanCommand.NotifyCanExecuteChanged();
        NukeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectAllChanged(bool value)
    {
        if (_isUpdatingSelection) return;
        foreach (var item in TrashDirectories)
        {
            item.IsSelected = value;
        }
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrashDirectoryViewModel.IsSelected))
        {
            UpdateSelectAllState();
            NukeCommand.NotifyCanExecuteChanged();
        }
    }

    private void UpdateSelectAllState()
    {
        if (TrashDirectories.Count == 0)
        {
            _isUpdatingSelection = true;
            SelectAll = false;
            _isUpdatingSelection = false;
            return;
        }
        
        _isUpdatingSelection = true;
        SelectAll = TrashDirectories.All(d => d.IsSelected);
        _isUpdatingSelection = false;
    }

    private bool CanScan() => !IsBusy;
    private bool CanNuke() => !IsBusy && TrashDirectories.Any(d => d.IsSelected);

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "Operation cancelled.";
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(RootPath) || !Directory.Exists(RootPath))
        {
            StatusText = "Please select a valid directory first.";
            return;
        }

        IsBusy = true;
        StatusText = "Scanning...";
        TrashDirectories.Clear();
        HasResults = false;
        
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var targets = new List<string>();
        if (ScanNodeModules) targets.Add("node_modules");
        if (ScanTarget) targets.Add("target");
        if (ScanBinObj)
        {
            targets.Add("bin");
            targets.Add("obj");
        }
        if (ScanBuildDist)
        {
            targets.Add("build");
            targets.Add("dist");
        }

        if (targets.Count == 0)
        {
            StatusText = "No target folders selected for scanning.";
            IsBusy = false;
            return;
        }

        var resultsList = new List<string>();
        
        try
        {
            IProgress<string> progress = new Progress<string>(s => StatusText = s);
            
            await Task.Run(() =>
            {
                ScanDirectory(RootPath, resultsList, targets, token, progress);
            }, token);

            if (token.IsCancellationRequested)
            {
                StatusText = "Scan cancelled.";
            }
            else
            {
                foreach (var path in resultsList)
                {
                    var item = new TrashDirectoryViewModel(path);
                    item.PropertyChanged += Item_PropertyChanged;
                    TrashDirectories.Add(item);
                }
                
                HasResults = TrashDirectories.Count > 0;
                StatusText = $"Scan finished. Found {TrashDirectories.Count} target folders.";
                UpdateSelectAllState();

                // Start calculating folder sizes in background
                _ = CalculateSizesAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanNuke))]
    private async Task NukeAsync()
    {
        var selected = TrashDirectories.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "No folders selected to nuke.";
            return;
        }

        IsBusy = true;
        StatusText = "Nuking directories...";
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        int deletedCount = 0;
        long totalFreed = 0;

        try
        {
            await Task.Run(() =>
            {
                foreach (var item in selected)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (Directory.Exists(item.Path))
                        {
                            Directory.Delete(item.Path, true);
                            deletedCount++;
                            totalFreed += item.SizeBytes;
                        }
                    }
                    catch (Exception)
                    {
                        // Skip if locked or access denied
                    }
                }
            }, token);

            // Refresh directory list
            for (int i = TrashDirectories.Count - 1; i >= 0; i--)
            {
                if (TrashDirectories[i].IsSelected && !Directory.Exists(TrashDirectories[i].Path))
                {
                    TrashDirectories.RemoveAt(i);
                }
            }

            HasResults = TrashDirectories.Count > 0;
            StatusText = $"Nuke completed. Deleted {deletedCount} folders, freed {ScanResult.FormatBytes(totalFreed)}.";
            UpdateSelectAllState();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Nuke operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nuke failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    private void ScanDirectory(string path, List<string> results, List<string> targets, CancellationToken token, IProgress<string> progress)
    {
        token.ThrowIfCancellationRequested();
        
        try
        {
            var dirs = Directory.EnumerateDirectories(path);
            foreach (var dir in dirs)
            {
                token.ThrowIfCancellationRequested();
                var name = Path.GetFileName(dir);
                
                bool isTarget = false;
                foreach (var t in targets)
                {
                    if (name.Equals(t, StringComparison.OrdinalIgnoreCase))
                    {
                        isTarget = true;
                        break;
                    }
                }
                
                if (isTarget)
                {
                    results.Add(dir);
                    progress.Report($"Found: {dir}");
                }
                else
                {
                    progress.Report($"Scanning: {dir}");
                    ScanDirectory(dir, results, targets, token, progress);
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception) { }
    }

    private async Task CalculateSizesAsync(CancellationToken token)
    {
        foreach (var item in TrashDirectories)
        {
            if (token.IsCancellationRequested) break;
            
            try
            {
                long size = await Task.Run(() => CalculateDirectorySize(item.Path, token), token);
                item.SizeBytes = size;
                item.SizeText = ScanResult.FormatBytes(size);
            }
            catch (OperationCanceledException)
            {
                item.SizeText = "Cancelled";
                break;
            }
            catch
            {
                item.SizeText = "Unknown";
            }
        }
    }

    private static long CalculateDirectorySize(string path, CancellationToken token)
    {
        long size = 0;
        try
        {
            var di = new DirectoryInfo(path);
            foreach (var fi in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                size += fi.Length;
            }
        }
        catch { }
        return size;
    }
}
