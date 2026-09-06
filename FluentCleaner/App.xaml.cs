using FluentCleaner.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FluentCleaner;

public partial class App : Application
{
    private bool _isWindowActivated;
    public MainWindow? MainWindow { get; private set; }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogException("AppDomain.UnhandledException", ex);
            Program.LogDiag($"[AppDomain-FATAL] {ex}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogException("TaskScheduler.UnobservedTaskException", e.Exception);
            Program.LogDiag($"[TaskScheduler-ERROR] {e.Exception}");
            e.SetObserved();
        };

        try
        {
            var lang = AppSettings.Instance.Language;
            if (!string.IsNullOrWhiteSpace(lang))
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
        }
        catch (Exception langEx)
        {
            Program.LogDiag($"[STARTUP-WARN] PrimaryLanguageOverride failed: {langEx.Message}");
        }

        InitializeComponent();

        UnhandledException += (_, e) =>
        {
            LogException("Application.UnhandledException", e.Exception);
            Program.LogDiag($"[Application.UnhandledException] (WindowActivated={_isWindowActivated}) {e.Exception}");
            System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.Exception}");

            // CRITICAL: Do NOT mark fatal startup exceptions as handled if window has not activated!
            // Marking e.Handled = true before window activation creates a headless zombie process.
            if (_isWindowActivated)
            {
                e.Handled = true;
            }
            else
            {
                Program.LogDiag("[FATAL-STARTUP] Unhandled exception occurred before MainWindow activation. Failing fast to prevent zombie process.");
                e.Handled = false;
            }
        };
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var dir = AppSettings.IsPortable
                ? AppContext.BaseDirectory
                : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluentCleaner");
            System.IO.Directory.CreateDirectory(dir);
            var logFile = System.IO.Path.Combine(dir, "app_error.log");
            System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] {source}: {ex}\nMessage: {ex?.Message}\n{ex?.StackTrace}\n---\n");
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Program.LogDiag("[BOOT] OnLaunched entered.");

            try
            {
                AppSettings.Reload();
                Program.LogDiag("[BOOT] AppSettings reloaded.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-WARN] AppSettings.Reload failed: {ex.Message}");
            }

            try
            {
                ResourceService.SetLanguage(AppSettings.Instance.Language);
                Program.LogDiag("[BOOT] Language configured.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-WARN] ResourceService.SetLanguage failed: {ex.Message}");
            }

            var cmdArgs = Environment.GetCommandLineArgs();
            bool isAuto     = cmdArgs.Any(a => a.Equals("/AUTO",     StringComparison.OrdinalIgnoreCase));
            bool isShutdown = cmdArgs.Any(a => a.Equals("/SHUTDOWN", StringComparison.OrdinalIgnoreCase));

            if (isAuto)
            {
                Program.LogDiag("[BOOT] Running in /AUTO silent mode.");
                _ = SilentRunner.RunAsync(isShutdown);
                return;
            }

            Program.LogDiag("[BOOT] Creating MainWindow...");
            MainWindow = new MainWindow();
            Program.LogDiag("[BOOT] MainWindow instantiated.");

            // Safe application of window decorations (cosmetic failures must never block window activation)
            try
            {
                SetupTitleBar();
                Program.LogDiag("[BOOT] TitleBar configured.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-WARN] SetupTitleBar failed: {ex.Message}");
            }

            try
            {
                RestoreWindowSize();
                Program.LogDiag("[BOOT] Window size restored.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-WARN] RestoreWindowSize failed: {ex.Message}");
            }

            try
            {
                ApplyBackdrop(AppSettings.Instance.Backdrop);
                Program.LogDiag("[BOOT] Backdrop applied.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-WARN] ApplyBackdrop failed: {ex.Message}");
            }

            try
            {
                ApplyTheme(AppSettings.Instance.Theme);
                Program.LogDiag("[BOOT] Theme applied.");
            }
            catch (Exception ex)
            {
                Program.LogDiag($"[BOOT-WARN] ApplyTheme failed: {ex.Message}");
            }

            Program.LogDiag("[BOOT] Calling MainWindow.Activate()...");
            MainWindow.Activate();
            _isWindowActivated = true;
            Program.LogDiag("[BOOT] MainWindow ACTIVATED successfully. UI is live.");

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
            Program.LogDiag($"[BOOT-FATAL] Fatal exception in OnLaunched: {ex}\n{ex.StackTrace}");
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
