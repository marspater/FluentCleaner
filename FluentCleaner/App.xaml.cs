using FluentCleaner.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentCleaner;

public partial class App : Application
{
    public MainWindow? MainWindow { get; private set; }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogException("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogException("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        try
        {
            var lang = AppSettings.Instance.Language;
            if (!string.IsNullOrWhiteSpace(lang))
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
        }
        catch { }

        InitializeComponent();

        UnhandledException += (_, e) =>
        {
            e.Handled = true; // prevent silent 0xC000027B process termination
            LogException("Application.UnhandledException", e.Exception);
            System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.Exception}");
        };
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var logFile = System.IO.Path.Combine(AppContext.BaseDirectory, "app_error.log");
            System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] {source}: {ex}\nMessage: {ex?.Message}\n{ex?.StackTrace}\n---\n");
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            AppSettings.Reload();

            ResourceService.SetLanguage(AppSettings.Instance.Language);

            var cmdArgs = Environment.GetCommandLineArgs();
            bool isAuto     = cmdArgs.Any(a => a.Equals("/AUTO",     StringComparison.OrdinalIgnoreCase));
            bool isShutdown = cmdArgs.Any(a => a.Equals("/SHUTDOWN", StringComparison.OrdinalIgnoreCase));

            if (isAuto)
            {
                _ = SilentRunner.RunAsync(isShutdown);
                return;
            }

            MainWindow = new MainWindow();
            SetupTitleBar();
            RestoreWindowSize();
            ApplyBackdrop(AppSettings.Instance.Backdrop);
            ApplyTheme(AppSettings.Instance.Theme);
            MainWindow.Activate();

            MainWindow.Closed += (_, _) =>
            {
                var size = MainWindow.AppWindow.Size;
                if (size.Width >= 600 && size.Height >= 400)
                {
                    AppSettings.Instance.WindowWidth  = size.Width;
                    AppSettings.Instance.WindowHeight = size.Height;
                    AppSettings.Instance.Save();
                }
            };
        }
        catch (Exception ex)
        {
            LogException("OnLaunched", ex);
            throw;
        }
    }

    private void SetupTitleBar()
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var bar = MainWindow!.AppWindow.TitleBar;
            bar.ButtonBackgroundColor         = Colors.Transparent;
            bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void RestoreWindowSize()
    {
        if (DisplayArea.GetFromWindowId(MainWindow!.AppWindow.Id, DisplayAreaFallback.Primary) is { } display)
        {
            var area = display.WorkArea;
            int maxW = Math.Max(600, area.Width - 40);
            int maxH = Math.Max(400, area.Height - 40);

            int w = Math.Clamp(AppSettings.Instance.WindowWidth, 600, maxW);
            int h = Math.Clamp(AppSettings.Instance.WindowHeight, 400, maxH);

            MainWindow.AppWindow.Resize(new SizeInt32(w, h));

            int posX = area.X + Math.Max(0, (area.Width - w) / 2);
            int posY = area.Y + Math.Max(0, (area.Height - h) / 2);

            MainWindow.AppWindow.Move(new PointInt32(posX, posY));
        }
        else
        {
            MainWindow.AppWindow.Resize(new SizeInt32(960, 620));
        }
    }

    public void ApplyTheme(string? theme)
    {
        var elementTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark"  => ElementTheme.Dark,
            _       => ElementTheme.Default
        };

        if (MainWindow?.Content is FrameworkElement root)
            root.RequestedTheme = elementTheme;

        if (MainWindow is not { } win) return;

        win.AppWindow.TitleBar.PreferredTheme = elementTheme switch
        {
            ElementTheme.Light => TitleBarTheme.Light,
            ElementTheme.Dark  => TitleBarTheme.Dark,
            _                  => TitleBarTheme.UseDefaultAppMode
        };
    }

    public void ApplyBackdrop(string? backdrop)
    {
        if (MainWindow is null) return;

        MainWindow.SystemBackdrop = backdrop?.ToLowerInvariant() switch
        {
            "acrylic" => new DesktopAcrylicBackdrop(),
            _         => new MicaBackdrop()
        };
    }
}
