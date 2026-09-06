using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FluentCleaner.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace FluentCleaner;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LogDiag("[STARTUP] FluentCleaner starting...");
        LogDiag($"[STARTUP] BaseDirectory: {AppContext.BaseDirectory}");
        AppDomain.CurrentDomain.ProcessExit += (_, _) => LogDiag("[SHUTDOWN] ProcessExit event fired.");

        try
        {
            ComWrappersSupport.InitializeComWrappers();
            LogDiag("[STARTUP] ComWrappers initialized.");

            Application.Start(p =>
            {
                try
                {
                    LogDiag("[STARTUP] Application.Start callback invoked.");
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    LogDiag("[STARTUP] DispatcherQueueSynchronizationContext set.");
                    new App();
                    LogDiag("[STARTUP] new App() completed.");
                }
                catch (Exception appEx)
                {
                    LogDiag($"[STARTUP-APP-ERROR] {appEx}");
                    throw;
                }
            });

            LogDiag("[STARTUP] Application.Start completed normally.");
        }
        catch (Exception ex)
        {
            LogDiag($"[STARTUP-FATAL] {ex}\n{ex.StackTrace}");
            throw;
        }
    }

    internal static void LogDiag(string message)
    {
        try
        {
            var dir = AppSettings.IsPortable
                ? AppContext.BaseDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluentCleaner");
            Directory.CreateDirectory(dir);
            var logFile = Path.Combine(dir, "startup_diag.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch
        {
            // Fail safely so diagnostics never prevent application startup
        }
    }
}
