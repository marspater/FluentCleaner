using System.Diagnostics;
using FluentCleaner.Services;
using FluentCleaner.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FluentCleaner.Views;

public sealed partial class SettingsPage : Page, IPageActions
{
    private static readonly HttpClient _http = new();
    private string? _updateVersion; // null = up to date, string = new version available
    private bool _pageReady; // true when the page has finished loading and is ready to handle events

    public SettingsPageViewModel ViewModel { get; } = new();
    public string AppVersion => AppInfo.DisplayVersion;
    public Visibility InsiderBadgeVisibility => AppInfo.IsInsider ? Visibility.Visible : Visibility.Collapsed;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _pageReady = false;
            ViewModel.Refresh();                                    //sync database toggles, paths, theme
            await CheckForUpdateAsync(silent: true);               //silent update check; banner only if newer version found
            ApiKeyBox.Password = AppSettings.Instance.GroqApiKey ?? ""; //pre-fill saved Groq key (masked)

            // Translator credit: hidden when the language file leaves it empty.
            var credit = ResourceService.Get("LblTranslatorCredit");
            var hasCredit = !string.IsNullOrWhiteSpace(credit) && credit != "LblTranslatorCredit";
            lblTranslatorCredit.Text       = hasCredit ? credit : "";
            lblTranslatorCredit.Visibility = hasCredit ? Visibility.Visible : Visibility.Collapsed;

            _pageReady = true;
        };
    }

    private async void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_pageReady) return;

        var result = await new ContentDialog
        {
            XamlRoot          = XamlRoot,
            RequestedTheme    = ActualTheme,
            Title             = ResourceService.Get("DlgRestartTitle"),
            Content           = ResourceService.Get("DlgRestartMessage"),
            PrimaryButtonText = ResourceService.Get("DlgRestartNow"),
            CloseButtonText   = ResourceService.Get("DlgRestartLater"),
            DefaultButton     = ContentDialogButton.Primary
        }.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            Process.Start(Environment.ProcessPath!);
            Application.Current.Exit();
        }
    }

    // --- Update check --------------------------------------------

    private async Task CheckForUpdateAsync(bool silent = false)
    {
        try
        {
            var latest = (await _http.GetStringAsync(AppLinks.VersionCheck))
                .Trim();

            _updateVersion = Version.TryParse(latest, out var remote) &&
                             Version.TryParse(AppInfo.VersionString, out var local) &&
                             remote > local ? latest : null;
        }
        catch { _updateVersion = null; }

        if (_updateVersion is not null)
        {
            UpdateBar.Severity = InfoBarSeverity.Error;
            UpdateBar.Title   = ResourceService.Fmt("St_UpdateAvailable", _updateVersion);
            UpdateBar.Message = ResourceService.Get("St_UpdateMessage");

            var btn = new Button { Content = ResourceService.Get("St_Download"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
            btn.Click += async (_, _) => await AppLinks.OpenAsync(AppLinks.Releases);
            UpdateBar.ActionButton = btn;
            UpdateBar.IsOpen = true;
        }
        else if (!silent)
        {
            UpdateBar.Severity     = InfoBarSeverity.Success;
            UpdateBar.Title        = ResourceService.Get("St_UpToDate");
            UpdateBar.Message      = ResourceService.Fmt("St_UpToDateMessage", AppInfo.DisplayVersion);
            UpdateBar.ActionButton = null;
            UpdateBar.IsOpen       = true;
        }
    }

    // --- IPageActions --------------------------------------------

    public void BuildActions(MenuFlyout flyout)
    {
        if (_updateVersion is not null)
        {
            var updateItem = new MenuFlyoutItem { Text = ResourceService.Fmt("St_MenuUpdate", _updateVersion) };
            updateItem.Click += async (_, _) => await AppLinks.OpenAsync(AppLinks.Releases);
            flyout.Items.Add(updateItem);
        }
        else
        {
            var checkItem = new MenuFlyoutItem { Text = ResourceService.Get("St_MenuCheckUpdates") };
            checkItem.Click += async (_, _) => await CheckForUpdateAsync();
            flyout.Items.Add(checkItem);
        }
    }

    private async void Link_GitHub(object sender, RoutedEventArgs e)   => await AppLinks.OpenAsync(AppLinks.GitHub);
    private async void Link_Issues(object sender, RoutedEventArgs e)   => await AppLinks.OpenAsync(AppLinks.Issues);
    private async void Link_Releases(object sender, RoutedEventArgs e) => await AppLinks.OpenAsync(AppLinks.Releases);
    private async void Link_Faq(object sender, RoutedEventArgs e)        => await AppLinks.OpenAsync(AppLinks.Faq);
    private async void Link_IconCredit(object sender, RoutedEventArgs e) => await AppLinks.OpenAsync(AppLinks.IconCredit);

    // saves trimmed key; null when the box is empty
    private void ApiKeySave_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Instance.GroqApiKey =
            string.IsNullOrWhiteSpace(ApiKeyBox.Password) ? null : ApiKeyBox.Password.Trim();
        AppSettings.Instance.Save();
    }

    // quick sanity-check
    private async void ApiKeyTest_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key)) { lblApiTestResult.Text = ResourceService.Get("St_ApiKeyMissing"); lblApiTestResult.Visibility = Visibility.Visible; return; }

        btnTestKey.IsEnabled        = false;
        lblApiTestResult.Text       = ResourceService.Get("St_ApiKeyTesting");
        lblApiTestResult.Visibility = Visibility.Visible;
        lblApiTestResult.Text       = await AiExplainer.TestKeyAsync(key);
        btnTestKey.IsEnabled        = true;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".ini");

        var hwnd = WindowNative.GetWindowHandle((Application.Current as App)?.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
            ViewModel.CustomPath = file.Path;
    }

    // --- Export / Import settings -----------------------------------------

    private async void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop, SuggestedFileName = "settings" };
        picker.FileTypeChoices.Add("JSON", [".json"]);

        var hwnd = WindowNative.GetWindowHandle((Application.Current as App)?.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            AppSettings.ExportTo(file.Path);
            ViewModel.StatusText = ResourceService.Fmt("St_ExportSuccess", file.Name);
        }
        catch (Exception ex) { ViewModel.StatusText = ResourceService.Fmt("St_ExportFailed", ex.Message); }
    }

    private async void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        picker.FileTypeFilter.Add(".json");

        var hwnd = WindowNative.GetWindowHandle((Application.Current as App)?.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            AppSettings.ImportFrom(file.Path);
            ViewModel.Refresh();
            ViewModel.StatusText = ResourceService.Fmt("St_ImportSuccess", file.Name);
        }
        catch (Exception ex) { ViewModel.StatusText = ResourceService.Fmt("St_ImportFailed", ex.Message); }
    }
}
