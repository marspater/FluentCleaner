using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FluentCleaner.ViewModels;
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
}
