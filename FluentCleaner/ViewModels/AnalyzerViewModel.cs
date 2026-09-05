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

    [RelayCommand(CanExecute = nameof(CanScan))]\n    private async Task ScanAsync()\n    {\n        if (string.IsNullOrWhiteSpace(RootPath) || !Directory.Exists(RootPath))\n        {\n            StatusText = \"Please select a valid directory first.\";\n            return;\n        }\n\n        IsBusy = true;\n        StatusText = \"Scanning and calculating sizes...\";\n        Items.Clear();\n        HasResults = false;\n        \n        _cts = new CancellationTokenSource();\n        var token = _cts.Token;\n\n        UpdateDriveInfo();\n\n        var tempItems = new List<(string name, string path, long size, bool isDir)>();\n        \n        try\n        {\n            IProgress<string> progress = new Progress<string>(s => StatusText = s);\n\n            await Task.Run(() =>\n            {\n                // Enumerate subdirectories\n                try\n                {\n                    var dirs = Directory.GetDirectories(RootPath);\n                    foreach (var dir in dirs)\n                    {\n                        token.ThrowIfCancellationRequested();\n                        progress.Report($\"Analyzing subdirectory: {Path.GetFileName(dir)}\");\n                        \n                        long size = CalculateDirectorySize(dir, token, progress);\n                        tempItems.Add((System.IO.Path.GetFileName(dir), dir, size, true));\n                    }\n                }\n                catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)\n                {\n                    System.Diagnostics.Debug.WriteLine($\"[AnalyzerViewModel.ScanAsync] Error enumerating directories in {RootPath}: {ex.Message}\");\n                }\n\n                // Enumerate files\n                try\n                {\n                    var files = Directory.EnumerateFiles(RootPath);\n                    foreach (var file in files)\n                    {\n                        token.ThrowIfCancellationRequested();\n                        long size = 0;\n                        try { size = new FileInfo(file).Length; }\n                        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)\n                        {\n                            System.Diagnostics.Debug.WriteLine($\"[AnalyzerViewModel.ScanAsync] Error getting file length for {file}: {ex.Message}\");\n                        }\n                        tempItems.Add((System.IO.Path.GetFileName(file), file, size, false));\n                    }\n                }\n                catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)\n                {\n                    System.Diagnostics.Debug.WriteLine($\"[AnalyzerViewModel.ScanAsync] Error enumerating files in {RootPath}: {ex.Message}\");\n                }\n\n            }, token);\n\n            if (token.IsCancellationRequested)\n            {\n                StatusText = \"Scan cancelled.\";\n            }\n            else\n            {\n                TotalScannedSize = tempItems.Sum(i => i.size);\n                \n                // Sort descending by size\n                var sorted = tempItems.OrderByDescending(i => i.size);\n                foreach (var i in sorted)\n                {\n                    Items.Add(new AnalyzerItemViewModel(i.name, i.path, i.size, i.isDir, TotalDriveSize, TotalScannedSize, this));\n                }\n\n                HasResults = Items.Count > 0;\n                StatusText = $\"Scan finished. Found {Items.Count} items. Total scanned size: {ScanResult.FormatBytes(TotalScannedSize)}.\";\n            }\n        }\n        catch (OperationCanceledException)\n        {\n            StatusText = \"Scan cancelled.\";\n        }\n        catch (Exception ex)\n        {\n            StatusText = $\"Scan failed: {ex.Message}\";\n        }\n        finally\n        {\n            IsBusy = false;\n            _cts = null;\n        }\n    }\n\n    private long CalculateDirectorySize(string path, CancellationToken token, IProgress<string> progress)\n    {\n        long size = 0;\n        try\n        {\n            var dirInfo = new DirectoryInfo(path);\n            foreach (var file in dirInfo.EnumerateFiles())\n            {\n                token.ThrowIfCancellationRequested();\n                size += file.Length;\n            }\n            \n            foreach (var subDir in dirInfo.EnumerateDirectories())\n            {\n                token.ThrowIfCancellationRequested();\n                if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0) continue;\n                \n                size += CalculateDirectorySize(subDir.FullName, token, progress);\n            }\n        }\n        catch (OperationCanceledException) { throw; }\n        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException or System.Security.SecurityException)\n        {\n            System.Diagnostics.Debug.WriteLine($\"[AnalyzerViewModel.CalculateDirectorySize] Error accessing path {path}: {ex.Message}\");\n        }\n        catch (Exception ex)\n        {\n            System.Diagnostics.Debug.WriteLine($\"[AnalyzerViewModel.CalculateDirectorySize] Unexpected error accessing path {path}: {ex}\");\n        }\n        return size;\n    }\n}\n