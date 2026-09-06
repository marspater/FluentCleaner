using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FluentCleaner.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace FluentCleaner.Views;

public sealed partial class AnalyzerView : Page
{
    public AnalyzerViewModel ViewModel { get; } = new();

    public AnalyzerView()
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
