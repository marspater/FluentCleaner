using FluentCleaner.Services;
using FluentCleaner.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace FluentCleaner;

public sealed partial class MainWindow : Window
{
    public string InsiderSubtitle => AppInfo.InsiderSubtitle;

    public MainWindow()
    {
        Program.LogDiag("[BOOT-WINDOW] MainWindow constructor started.");
        InitializeComponent();
        Program.LogDiag("[BOOT-WINDOW] InitializeComponent completed.");

        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            Program.LogDiag("[BOOT-WINDOW] TitleBar customized.");
        }
        catch (Exception ex)
        {
            Program.LogDiag($"[BOOT-WINDOW-WARN] TitleBar customization issue: {ex.Message}");
        }

        // Navigate to initial CleanerPage on NavView.Loaded so MainWindow completes construction and activates immediately
        NavView.Loaded += (_, _) =>
        {
            try
            {
                if (NavFrame.Content is null && NavView.MenuItems.Count > 0)
                {
                    Program.LogDiag("[BOOT-WINDOW] NavView loaded. Navigating to initial CleanerPage...");
                    NavView.SelectedItem = NavView.MenuItems[0];
                    NavFrame.Navigate(typeof(CleanerPage), null, new SuppressNavigationTransitionInfo());
                    SyncSearchState();
                    Program.LogDiag("[BOOT-WINDOW] Initial navigation to CleanerPage completed.");
                }
            }
            catch (Exception navEx)
            {
                Program.LogDiag($"[BOOT-WINDOW-ERROR] Initial navigation failed: {navEx}");
            }
        };

        SizeChanged += MainWindow_SizeChanged;
        try
        {
            UpdateTitleSearch(AppWindow.Size.Width);
        }
        catch { }

        Program.LogDiag("[BOOT-WINDOW] MainWindow constructor completed.");
    }

    // --- TitleBar pane toggle -------------------------------------------------

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args) =>
        NavView.IsPaneOpen = !NavView.IsPaneOpen;

    // --- Navigation -----------------------------------------------------------

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        TitleSearchBox.Text = "";
        var prevContent = NavFrame.Content;  // capture before navigation
        if (prevContent is ISearchablePage old)
            old.OnSearch("");

        var transition = new DrillInNavigationTransitionInfo();

        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage), null, transition);
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "Cleaner":
                    if (NavFrame.Content is not CleanerPage)
                        NavFrame.Navigate(typeof(CleanerPage), null, transition);
                    break;
                case "Tools":
                    if (NavFrame.Content is not ToolsPage)
                        NavFrame.Navigate(typeof(ToolsPage), null, transition);
                    break;
                case "Analyzer":
                    if (NavFrame.Content is not AnalyzerView)
                        NavFrame.Navigate(typeof(AnalyzerView), null, transition);
                    break;
                case "Developer":
                    if (NavFrame.Content is not DeveloperCleanupView)
                        NavFrame.Navigate(typeof(DeveloperCleanupView), null, transition);
                    break;
                case "Terminal":
                    if (NavFrame.Content is not TerminalPage)
                        NavFrame.Navigate(typeof(TerminalPage), null, transition);
                    break;
                case "Custom":
                    if (NavFrame.Content is not CustomPage)
                        NavFrame.Navigate(typeof(CustomPage), null, transition);
                    break;
            }
        }

        SyncSearchState();
    }

    // --- Page actions flyout --------------------------------------------------

    private void PageActionsFlyout_Opening(object sender, object e)
    {
        PageActionsFlyout.Items.Clear();

        if (NavFrame.Content is IPageActions provider)
            provider.BuildActions(PageActionsFlyout);
        else
        {
            PageActionsFlyout.Items.Add(new MenuFlyoutItem
            {
                Text = ResourceService.Get("St_NoActions"),
                IsEnabled = false
            });
        }
    }

    // --- Search ---------------------------------------------------------------

    private void SyncSearchState()
    {
        bool searchable = NavFrame.Content is ISearchablePage;
        TitleSearchBox.IsEnabled = searchable;
        SearchIconButton.IsEnabled = searchable;
    }

    private void TitleSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput
            && NavFrame.Content is ISearchablePage page)
            page.OnSearch(sender.Text);
    }

    // --- Search collapse -----------------------------------------------------

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args) =>
        UpdateTitleSearch(args.Size.Width);

    private void UpdateTitleSearch(double width)
    {
        bool compact = width < 560;
        TitleSearchBox.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SearchIconButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;

        if (!compact)
            TitleSearchBox.Width = width < 700 ? 220 : 280;
    }
}