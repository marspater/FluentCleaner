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

public partial class AnalyzerItemViewModel : ObservableObject
{
    private readonly AnalyzerViewModel _parent;

    public string Name { get; }
    public string FullPath { get; }
    public long SizeBytes { get; }
    public bool IsDirectory { get; }
    
    public string FormattedSize => ScanResult.FormatBytes(SizeBytes);
    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE8A5"; // Folder vs File (Segoe MDL2 Assets)

    public double PercentageOfDrive { get; }
    public double PercentageOfScan { get; }

    public double PercentageValue => _parent.UseScannedRootPercentage ? PercentageOfScan : PercentageOfDrive;
    
    public string SizeAndPercentageText => $"{FormattedSize} ({PercentageValue:F1}%)";

    public void NotifyPercentageChanged()
    {
        OnPropertyChanged(nameof(PercentageValue));
        OnPropertyChanged(nameof(SizeAndPercentageText));
    }

    public AnalyzerItemViewModel(string name, string path, long sizeBytes, bool isDirectory, long totalDriveSize, long totalScannedSize, AnalyzerViewModel parent)
    {
        Name = name;
        FullPath = path;
        SizeBytes = sizeBytes;
        IsDirectory = isDirectory;
        _parent = parent;

        PercentageOfDrive = totalDriveSize > 0 ? (sizeBytes / (double)totalDriveSize) * 100 : 0;
        PercentageOfScan = totalScannedSize > 0 ? (sizeBytes / (double)totalScannedSize) * 100 : 0;
    }
}

public partial class AnalyzerViewModel : ObservableObject
{
    [ObservableProperty] public partial string RootPath { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "Select a drive or directory to analyze.";
    [ObservableProperty] public partial bool HasResults { get; set; }
    [ObservableProperty] public partial bool UseScannedRootPercentage { get; set; } = true;
    [ObservableProperty] public partial string DriveInfoText { get; set; } = "";

    public bool IsNotBusy => !IsBusy;

    public ObservableCollection<AnalyzerItemViewModel> Items { get; } = new();
    public ObservableCollection<string> LocalDrives { get; } = new();

    public long TotalDriveSize { get; private set; }
    public long TotalScannedSize { get; private set; }

    private CancellationTokenSource? _cts;

    public AnalyzerViewModel()
    {
        LoadDrives();
    }

    private void LoadDrives()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                                  .Where(d => d.IsReady)
                                  .Select(d => d.Name);
            
            foreach (var d in drives)
            {
                LocalDrives.Add(d);
            }

            if (LocalDrives.Count > 0)
            {
                RootPath = LocalDrives[0];
            }
        }
        catch { }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        ScanCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseScannedRootPercentageChanged(bool value)
    {
        foreach (var item in Items)
        {
            item.NotifyPercentageChanged();
        }
    }

    private void UpdateDriveInfo()
    {
        try
        {
            var driveName = Path.GetPathRoot(RootPath);
            if (!string.IsNullOrEmpty(driveName))
            {
                var driveInfo = new DriveInfo(driveName);
                TotalDriveSize = driveInfo.TotalSize;
                long freeSpace = driveInfo.AvailableFreeSpace;
                DriveInfoText = $"Drive Capacity: {ScanResult.FormatBytes(TotalDriveSize)}  ·  Free Space: {ScanResult.FormatBytes(freeSpace)}";
            }
            else
            {
                TotalDriveSize = 0;
                DriveInfoText = "";
            }
        }
        catch
        {
            TotalDriveSize = 0;
            DriveInfoText = "";
        }
    }

    private bool CanScan() => !IsBusy;

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
        StatusText = "Scanning and calculating sizes...";
        Items.Clear();
        HasResults = false;
        
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        UpdateDriveInfo();

        var tempItems = new List<(string name, string path, long size, bool isDir)>();
        
        try
        {
            IProgress<string> progress = new Progress<string>(s => StatusText = s);

            await Task.Run(() =>
            {
                // Enumerate subdirectories
                try
                {
                    var dirs = Directory.GetDirectories(RootPath);
                    foreach (var dir in dirs)
                    {
                        token.ThrowIfCancellationRequested();
                        progress.Report($"Analyzing subdirectory: {Path.GetFileName(dir)}");
                        
                        long size = CalculateDirectorySize(dir, token, progress);
                        tempItems.Add((System.IO.Path.GetFileName(dir), dir, size, true));
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }

                // Enumerate files
                try
                {
                    var files = Directory.EnumerateFiles(RootPath);
                    foreach (var file in files)
                    {
                        token.ThrowIfCancellationRequested();
                        long size = 0;
                        try { size = new FileInfo(file).Length; } catch { }
                        tempItems.Add((System.IO.Path.GetFileName(file), file, size, false));
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }

            }, token);

            if (token.IsCancellationRequested)
            {
                StatusText = "Scan cancelled.";
            }
            else
            {
                TotalScannedSize = tempItems.Sum(i => i.size);
                
                // Sort descending by size
                var sorted = tempItems.OrderByDescending(i => i.size);
                foreach (var i in sorted)
                {
                    Items.Add(new AnalyzerItemViewModel(i.name, i.path, i.size, i.isDir, TotalDriveSize, TotalScannedSize, this));
                }

                HasResults = Items.Count > 0;
                StatusText = $"Scan finished. Found {Items.Count} items. Total scanned size: {ScanResult.FormatBytes(TotalScannedSize)}.";
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

    private long CalculateDirectorySize(string path, CancellationToken token, IProgress<string> progress)
    {
        long size = 0;
        try
        {
            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.EnumerateFiles())
            {
                token.ThrowIfCancellationRequested();
                size += file.Length;
            }
            
            foreach (var subDir in dirInfo.EnumerateDirectories())
            {
                token.ThrowIfCancellationRequested();
                if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                
                size += CalculateDirectorySize(subDir.FullName, token, progress);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception) { }
        return size;
    }
}
