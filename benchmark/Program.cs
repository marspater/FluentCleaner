using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string dummyFile = "test_winapp2.ini";
        // Create a test file with 15,000 lines (simulating Winapp2.ini)
        using (var writer = new StreamWriter(dummyFile))
        {
            for (int i = 0; i < 15000; i++)
            {
                if (i % 3 == 0) writer.WriteLine($"[App {i}]");
                else writer.WriteLine($"FileKey{i}=C:\\Test\\*.*");
            }
        }

        var fi = new FileInfo(dummyFile);

        // Warmup
        var warmupLines = File.ReadLines(dummyFile).Count(l => l.StartsWith('[') && !l.StartsWith("[Winapp2"));

        int iterations = 100;

        // Baseline: Sync File.ReadLines on UI/Main thread
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var lines = File.ReadLines(dummyFile).Count(l => l.StartsWith('[') && !l.StartsWith("[Winapp2"));
            var info = $"{lines} entries | {fi.Length / 1024} KB | {fi.LastWriteTime:yyyy-MM-dd}";
        }
        sw.Stop();
        double syncMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"Baseline Sync File.ReadLines: {sw.ElapsedMilliseconds} ms total ({syncMsPerOp:F3} ms/op)");

        // Optimized: Async offloaded via Task.Run
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            var info = await Task.Run(() =>
            {
                if (!File.Exists(dummyFile)) return "Not downloaded";
                var fInfo = new FileInfo(dummyFile);
                var lines = File.ReadLines(dummyFile).Count(l => l.StartsWith('[') && !l.StartsWith("[Winapp2"));
                return $"{lines} entries | {fInfo.Length / 1024} KB | {fInfo.LastWriteTime:yyyy-MM-dd}";
            });
        }
        sw.Stop();
        double asyncMsPerOp = sw.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"Optimized Task.Run Async File Reading: {sw.ElapsedMilliseconds} ms total ({asyncMsPerOp:F3} ms/op)");

        if (File.Exists(dummyFile)) File.Delete(dummyFile);
    }
}
