using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FluentCleaner.Models;
using FluentCleaner.Services;
using FluentCleaner.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace FluentCleaner.Views;

public sealed partial class DeveloperCleanupView : Page
{
    public DeveloperCleanupViewModel ViewModel { get; } = new();

    public DeveloperCleanupView()
    {
        InitializeComponent();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");

            var mainWindow = (Application.Current as App)?.MainWindow;
            if (mainWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            }

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ViewModel.RootPath = folder.Path;
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Failed to open folder picker: {ex.Message}";
        }
    }

    private async void NukeButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.TrashDirectories.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (XamlRoot is null)
        {
            await ViewModel.NukeCommand.ExecuteAsync(null);
            return;
        }

        var totalBytes = selected.Sum(d => d.SizeBytes);
        var sizeFormatted = ScanResult.FormatBytes(totalBytes);

        var dialog = new ContentDialog
        {
            XamlRoot          = XamlRoot,
            RequestedTheme    = ActualTheme,
            CornerRadius      = new CornerRadius(8),
            Title             = "Confirm Nuke",
            PrimaryButtonText = "Nuke Folders",
            CloseButtonText   = "Cancel",
            DefaultButton     = ContentDialogButton.Close,
            Content           = new TextBlock
            {
                Text = $"Are you sure you want to permanently delete {selected.Count} selected build directories (~{sizeFormatted})?\n\nThis cannot be undone.",
                TextWrapping = TextWrapping.Wrap
            }
        };

        if (await DialogHelper.ShowSafeAsync(dialog) == ContentDialogResult.Primary)
        {
            await ViewModel.NukeCommand.ExecuteAsync(null);
        }
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem { Tag: string path } && (Directory.Exists(path) || File.Exists(path)))
            {
                Process.Start("explorer.exe", path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenExplorer_Click] Error: {ex.Message}");
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem { Tag: string path } && !string.IsNullOrEmpty(path))
            {
                var dp = new DataPackage();
                dp.SetText(path);
                Clipboard.SetContent(dp);
                ViewModel.StatusText = $"Copied path to clipboard: {path}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CopyPath_Click] Error: {ex.Message}");
        }
    }
}
