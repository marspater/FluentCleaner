using FluentCleaner.Services;
using FluentCleaner.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FluentCleaner.Views;

public sealed partial class CleanerPage : Page, ISearchablePage, IPageActions
{
    public CleanerPageViewModel ViewModel { get; } = new();

    private bool _loaded;

    public CleanerPage()
    {
        InitializeComponent();
        // Auto-load on first appearance using whatever path settings resolves to
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                Program.LogDiag("[BOOT-CLEANERPAGE] Loaded handler started.");
                AppSettings.Reload();
                var paths = AppSettings.Instance.ResolveDatabasePaths().ToList();
                if (paths.Count == 0) paths.Add(Path.Combine(AppContext.BaseDirectory, "Winapp2.ini"));
                Program.LogDiag($"[BOOT-CLEANERPAGE] Loading {paths.Count} database paths...");
                await ViewModel.LoadWinapp2Async(paths);
                Program.LogDiag("[BOOT-CLEANERPAGE] LoadWinapp2Async completed.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-CLEANERPAGE-ERROR] Exception in Loaded handler: {ex}");
            }
        };
    }

    // OnNavigatedTo fires on every visit, even with NavigationCacheMode="Required".
    // Loaded only fires once — so this is the right place to pick up new custom entries.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_loaded)
            _ = ViewModel.RefreshCustomEntriesAsync();
    }

    // --- ISearchablePage ----------------------------------------------------------
    public void OnSearch(string text) => ViewModel.SearchText = text;

    // --- IPageActions ----------------------------------------------------------
    public void BuildActions(MenuFlyout flyout)
    {
        void Add(string label, Action action)
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) => action();
            flyout.Items.Add(item);
        }

        Add(ResourceService.Get("St_MenuSelectAll"),      () => ViewModel.SelectAllCommand.Execute(null));
        Add(ResourceService.Get("St_MenuSelectNone"),     () => ViewModel.SelectNoneCommand.Execute(null));
        Add(ResourceService.Get("St_MenuSelectDefaults"), () => ViewModel.SelectDefaultsCommand.Execute(null));
        flyout.Items.Add(new MenuFlyoutSeparator());
        Add(ResourceService.Get("St_MenuExpandAll"),      () => ViewModel.ExpandAllCommand.Execute(null));
        Add(ResourceService.Get("St_MenuCollapseAll"),    () => ViewModel.CollapseAllCommand.Execute(null));
        flyout.Items.Add(new MenuFlyoutSeparator());
        Add(ResourceService.Get("St_MenuSortDesc"),       () => ViewModel.SortResultsDescCommand.Execute(null));
        Add(ResourceService.Get("St_MenuSortAsc"),        () => ViewModel.SortResultsAscCommand.Execute(null));
        flyout.Items.Add(new MenuFlyoutSeparator());
        Add(ResourceService.Get("St_MenuRefresh"),        () => ViewModel.RefreshCommand.Execute(null));
    }

    // Open the detail view for the clicked result row
    private void ResultsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScanResultLine line && line.Result is not null)
            ViewModel.SelectedResultLine = line;
    }

    // Click a path row in the detail list;just highlight the file in Explorer.
    // Headers and registry keys are ignored; only real file paths get /select treatment.
    private void DetailList_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not DetailLine { IsHeader: false } line) return;
            var path = line.Text;
            if (string.IsNullOrWhiteSpace(path) || path.Contains('"') || path.StartsWith("HK", StringComparison.OrdinalIgnoreCase)) return;

            if (File.Exists(path) || Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DetailList_ItemClick] Error: {ex.Message}");
        }
    }

    // Right-click "Exclude file";protects just this one file (FILE|dir|name)
    private void ExcludeFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuFlyoutItem { Tag: string path } || string.IsNullOrWhiteSpace(path)) return;
            if (path.StartsWith("HK", StringComparison.OrdinalIgnoreCase)) return;

            var dir  = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file)) return;

            AddGlobalExclusion($"FILE|{dir}|{file}", ResourceService.Fmt("St_ExcludedFile", file));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExcludeFile_Click] Error: {ex.Message}");
        }
    }

    // Right-click "Exclude folder";protects the entire parent folder tree (PATH|dir)
    private void ExcludeFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuFlyoutItem { Tag: string path } || string.IsNullOrWhiteSpace(path)) return;
            if (path.StartsWith("HK", StringComparison.OrdinalIgnoreCase)) return;

            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir)) return;

            AddGlobalExclusion($"PATH|{dir}", ResourceService.Fmt("St_ExcludedFolder", dir));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExcludeFolder_Click] Error: {ex.Message}");
        }
    }

    private void AddGlobalExclusion(string rule, string status)
    {
        try
        {
            var settings = Services.AppSettings.Instance;
            if (settings.GlobalExclusions.Contains(rule, StringComparer.OrdinalIgnoreCase)) return;

            settings.GlobalExclusions.Add(rule);
            settings.GlobalExclusionsEnabled = true;
            settings.Save();
            ViewModel.StatusText = status;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddGlobalExclusion] Error: {ex.Message}");
        }
    }

    // Entry flyout; Tag="{x:Bind}" gives us the CleanerEntryViewModel directly
    private async void EntryAnalyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem { Tag: CleanerEntryViewModel vm })
                await ViewModel.AnalyzeSingleEntryAsync(vm);
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Analysis error: {ex.Message}";
        }
    }

    private async void EntryClean_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem { Tag: CleanerEntryViewModel vm })
            {
                if (ViewModel.IsBusy) return;
                if (!await CheckRunningBrowsersAsync([vm])) return;
                if (ViewModel.IsBusy) return;
                if (!await ConfirmWarningsAsync(ViewModel.GetWarningsForEntry(vm)))
                    return;
                if (ViewModel.IsBusy) return;

                await ViewModel.CleanSingleEntryAsync(vm);
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Cleaning error: {ex.Message}";
        }
    }

    // Ask Groq to explain the entry;result is cached so repeated opens are instant
    private async void EntryExplain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: CleanerEntryViewModel vm } || XamlRoot is null) return;

        try
        {
            var textBlock = new TextBlock
            {
                Text = ResourceService.Get("DlgExplainThinking"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };

            var dialog = new ContentDialog
            {
                XamlRoot        = XamlRoot,
                RequestedTheme  = ActualTheme,
                CornerRadius    = new CornerRadius(8),
                Title           = vm.Name,
                CloseButtonText = ResourceService.Get("DlgExplainClose"),
                Content         = textBlock
            };

            var explainTask = AiExplainer.ExplainAsync(vm.Entry);
            var showTask = DialogHelper.ShowSafeAsync(dialog);
            textBlock.Text = await explainTask;
            await showTask;
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Explain error: {ex.Message}";
        }
    }


    // Category flyout;same trick with CleanerCategoryViewModel
    private async void CatAnalyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem { Tag: CleanerCategoryViewModel vm })
                await ViewModel.AnalyzeCategoryAsync(vm);
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Category analysis error: {ex.Message}";
        }
    }

    private async void CatClean_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem { Tag: CleanerCategoryViewModel vm })
            {
                if (ViewModel.IsBusy) return;
                var selected = vm.Entries.Where(e => e.IsSelected).ToList();
                if (!await CheckRunningBrowsersAsync(selected)) return;
                if (ViewModel.IsBusy) return;
                if (!await ConfirmWarningsAsync(ViewModel.GetWarningsForCategory(vm)))
                    return;
                if (ViewModel.IsBusy) return;

                await ViewModel.CleanCategoryAsync(vm);
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Category clean error: {ex.Message}";
        }
    }

    private async void RunCleaner_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ViewModel.RunCleanerCommand.CanExecute(null))
                return;

            if (!await CheckRunningBrowsersAsync()) return;
            if (!ViewModel.RunCleanerCommand.CanExecute(null)) return;
            if (!await ConfirmWarningsAsync(ViewModel.GetWarningsForSelectedEntries()))
                return;
            if (!ViewModel.RunCleanerCommand.CanExecute(null)) return;

            await ((IAsyncRelayCommand)ViewModel.RunCleanerCommand).ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = $"Cleaner error: {ex.Message}";
        }
    }

    // Check for running browsers;only warns when browser entries are actually selected
    private static readonly (string Process, string DisplayName, int[] LangSecRefs)[] KnownBrowsers =
    [
        ("chrome",  "Google Chrome",    [3029]),
        ("firefox", "Mozilla Firefox",  [3026]),
        ("msedge",  "Microsoft Edge",   [3006]),
        ("opera",   "Opera",            [3027, 3035]),
        ("brave",   "Brave",            [3034]),
        ("vivaldi", "Vivaldi",          [3033]),
    ];

    private async Task<bool> CheckRunningBrowsersAsync(IEnumerable<CleanerEntryViewModel>? selectedEntries = null)
    {
        if (XamlRoot is null) return true;

        var selectedCodes = (selectedEntries ?? ViewModel.FlatEntries.Where(e => e.IsSelected))
            .Select(e => e.Entry.LangSecRef ?? -1)
            .ToHashSet();

        var running = KnownBrowsers
            .Where(b => b.LangSecRefs.Any(selectedCodes.Contains))
            .Where(b => System.Diagnostics.Process.GetProcessesByName(b.Process).Length > 0)
            .Select(b => b.DisplayName)
            .ToList();

        if (running.Count == 0)
            return true;

        var dialog = new ContentDialog
        {
            XamlRoot          = XamlRoot,
            RequestedTheme    = ActualTheme,
            CornerRadius      = new CornerRadius(8),
            Title             = ResourceService.Get("DlgBrowsersTitle"),
            PrimaryButtonText = ResourceService.Get("DlgBrowsersContinue"),
            CloseButtonText   = ResourceService.Get("DlgBrowsersCancel"),
            DefaultButton     = ContentDialogButton.Close,
            Content           = ResourceService.Fmt("DlgBrowsersMessage", string.Join(", ", running))
        };

        return await DialogHelper.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
    }

    // Show a warning dialog if any of the selected entries have warnings;return true to proceed with cleaning
    private async Task<bool> ConfirmWarningsAsync(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0 || XamlRoot is null)
            return true;

        var dialog = new ContentDialog
        {
            XamlRoot          = XamlRoot,
            RequestedTheme    = ActualTheme,
            CornerRadius      = new CornerRadius(8),
            Title             = ResourceService.Get("DlgWarningTitle"),
            PrimaryButtonText = ResourceService.Get("DlgWarningContinue"),
            CloseButtonText   = ResourceService.Get("DlgWarningCancel"),
            DefaultButton     = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text =
                        ResourceService.Get("DlgWarningMessage") +
                        $"{Environment.NewLine}{Environment.NewLine}" +
                        string.Join($"{Environment.NewLine}{Environment.NewLine}", warnings),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        return await DialogHelper.ShowSafeAsync(dialog) == ContentDialogResult.Primary;
    }
}
