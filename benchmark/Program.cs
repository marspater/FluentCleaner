using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    private static readonly Regex RxFileKey    = new(@"^FileKey\d+$",    RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxRegKey     = new(@"^RegKey\d+$",     RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxExcludeKey = new(@"^ExcludeKey\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxDetect     = new(@"^Detect\d*$",     RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxDetectFile = new(@"^DetectFile\d*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static void Main()
    {
        if (!File.Exists("Winapp2.ini"))
        {
            Console.WriteLine("Winapp2.ini not found!");
            return;
        }

        string content = File.ReadAllText("Winapp2.ini");
        Console.WriteLine($"Winapp2.ini size: {content.Length} chars");

        // Warmup
        ParseOriginal(content);
        ParseOptimized(content);

        int runs = 50;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memBeforeOriginal = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
        {
            ParseOriginal(content);
        }
        sw.Stop();
        long memAfterOriginal = GC.GetTotalAllocatedBytes(true);
        double origTime = sw.ElapsedMilliseconds / (double)runs;
        double origAlloc = (memAfterOriginal - memBeforeOriginal) / (double)runs / 1024.0 / 1024.0;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memBeforeOpt = GC.GetTotalAllocatedBytes(true);
        sw.Restart();
        for (int i = 0; i < runs; i++)
        {
            ParseOptimized(content);
        }
        sw.Stop();
        long memAfterOpt = GC.GetTotalAllocatedBytes(true);
        double optTime = sw.ElapsedMilliseconds / (double)runs;
        double optAlloc = (memAfterOpt - memBeforeOpt) / (double)runs / 1024.0 / 1024.0;

        Console.WriteLine($"Original parser: {origTime:F2} ms, Allocations: {origAlloc:F2} MB per run");
        Console.WriteLine($"Optimized parser: {optTime:F2} ms, Allocations: {optAlloc:F2} MB per run");
        Console.WriteLine($"Speedup: {origTime / optTime:F2}x faster");
        Console.WriteLine($"Memory reduction: {origAlloc / optAlloc:F2}x less memory allocated");
    }

    static int ParseOriginal(string content)
    {
        int count = 0;
        foreach (var rawLine in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                count++;
                continue;
            }

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.Length == 0) continue;

            if (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("Section", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("Warning", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("Default", StringComparison.OrdinalIgnoreCase)) { }
            else if (RxDetect.IsMatch(key)) { }
            else if (RxDetectFile.IsMatch(key)) { }
            else if (RxFileKey.IsMatch(key)) { }
            else if (RxRegKey.IsMatch(key)) { }
            else if (RxExcludeKey.IsMatch(key)) { }
        }
        return count;
    }

    static int ParseOptimized(string content)
    {
        int count = 0;
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                count++;
                continue;
            }

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.Length == 0) continue;

            if (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("Section", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("Warning", StringComparison.OrdinalIgnoreCase)) { }
            else if (key.Equals("Default", StringComparison.OrdinalIgnoreCase)) { }
            else if (IsKey(key, "Detect")) { }
            else if (IsKey(key, "DetectFile")) { }
            else if (IsKeyWithDigits(key, "FileKey")) { }
            else if (IsKeyWithDigits(key, "RegKey")) { }
            else if (IsKeyWithDigits(key, "ExcludeKey")) { }
        }
        return count;
    }

    static bool IsKey(ReadOnlySpan<char> key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key[prefix.Length..];
        for (int i = 0; i < suffix.Length; i++)
        {
            if (!char.IsAsciiDigit(suffix[i])) return false;
        }
        return true;
    }

    static bool IsKeyWithDigits(ReadOnlySpan<char> key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key[prefix.Length..];
        if (suffix.Length == 0) return false;
        for (int i = 0; i < suffix.Length; i++)
        {
            if (!char.IsAsciiDigit(suffix[i])) return false;
        }
        return true;
    }
}
