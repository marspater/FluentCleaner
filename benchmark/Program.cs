using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var CustomDir = "CustomTest";
        if (!Directory.Exists(CustomDir))
        {
            Directory.CreateDirectory(CustomDir);
            for(int i = 0; i < 10000; i++)
            {
                File.WriteAllText(Path.Combine(CustomDir, $"test_{i}.ini"), "");
            }
        }

        // Warmup
        var filesWarmup = Directory.GetFiles(CustomDir, "*.ini").ToList();

        var sw = Stopwatch.StartNew();
        var files = Directory.GetFiles(CustomDir, "*.ini")
                             .Where(f => !f.EndsWith(".ini.disabled", StringComparison.OrdinalIgnoreCase))
                             .ToList();
        sw.Stop();
        Console.WriteLine($"Sync Directory.GetFiles: {sw.ElapsedMilliseconds} ms, ticks: {sw.ElapsedTicks}");

        sw.Restart();
        var files4 = Directory.EnumerateFiles(CustomDir, "*.ini")
                             .Where(f => !f.EndsWith(".ini.disabled", StringComparison.OrdinalIgnoreCase))
                             .ToList();
        sw.Stop();
        Console.WriteLine($"Sync Directory.EnumerateFiles: {sw.ElapsedMilliseconds} ms, ticks: {sw.ElapsedTicks}");

        sw.Restart();
        var files2 = await Task.Run(() => Directory.GetFiles(CustomDir, "*.ini")
                             .Where(f => !f.EndsWith(".ini.disabled", StringComparison.OrdinalIgnoreCase))
                             .ToList());
        sw.Stop();
        Console.WriteLine($"Async Task.Run with GetFiles: {sw.ElapsedMilliseconds} ms, ticks: {sw.ElapsedTicks}");

        sw.Restart();
        var files3 = await Task.Run(() => Directory.EnumerateFiles(CustomDir, "*.ini")
                             .Where(f => !f.EndsWith(".ini.disabled", StringComparison.OrdinalIgnoreCase))
                             .ToList());
        sw.Stop();
        Console.WriteLine($"Async Task.Run with EnumerateFiles: {sw.ElapsedMilliseconds} ms, ticks: {sw.ElapsedTicks}");

        Directory.Delete(CustomDir, true);
    }
}
