using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FluentCleaner;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string logDir = AppContext.BaseDirectory;
        File.WriteAllText(Path.Combine(logDir, "FCleaner_entrypoint.log"), "Main started!\n");
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        string logFile = Path.Combine(logDir, "FluentCleaner_startup.log");
        try
        {
            File.WriteAllText(logFile, $"Starting FluentCleaner at {DateTime.Now}...\n");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.AppendAllText(logFile, $"UnhandledException in AppDomain: {e.ExceptionObject}\n");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                File.AppendAllText(logFile, $"UnobservedTaskException: {e.Exception}\n");
                e.SetObserved();
            };

            try
            {
                File.AppendAllText(logFile, "Initializing Bootstrapper for WinAppSDK 2.x...\n");
                // 0x00020000 corresponds to Major=2, Minor=0
                bool success = Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(0x00020000, out var hresult);
                File.AppendAllText(logFile, $"Bootstrapper initialized: {success}, HRESULT: {hresult:X}\n");
            }
            catch (Exception bEx)
            {
                File.AppendAllText(logFile, $"Bootstrapper skipped/failed (SelfContained mode active): {bEx.Message}\n");
            }

            StartWinUI(logFile);
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile, $"FATAL EXCEPTION in Main: {ex}\n");
        }
    }

    // Move WinRT types to a separate method to prevent JIT from failing before Bootstrapper runs.
    private static void StartWinUI(string logFile)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        File.AppendAllText(logFile, "ComWrappers initialized.\n");

        File.AppendAllText(logFile, "Calling Application.Start...\n");
        Application.Start((p) =>
        {
            try
            {
                File.AppendAllText(logFile, "Application.Start callback invoked.\n");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, $"Exception in Application.Start callback: {ex}\n");
                throw;
            }
        });
        File.AppendAllText(logFile, "Application.Start returned.\n");
    }
}
