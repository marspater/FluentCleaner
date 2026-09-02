using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using FluentCleaner.Services;

namespace FluentCleaner.Benchmark;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== 1. Syntax Check ===");
        RunSyntaxCheck();

        Console.WriteLine("\n=== 2. Correctness Tests ===");
        RunCorrectnessTests();

        Console.WriteLine("\n=== 3. Performance Benchmark ===");
        RunPerformanceBenchmark();
    }

    static void RunSyntaxCheck()
    {
        var rootDir = Directory.Exists("FluentCleaner") ? "." : "..";
        var fcDir = Path.Combine(rootDir, "FluentCleaner");
        var testsDir = Path.Combine(rootDir, "FluentCleaner.Tests");

        var files = Directory.GetFiles(fcDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories));

        int errorCount = 0;
        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
            if (diagnostics.Count > 0)
            {
                errorCount += diagnostics.Count;
                Console.WriteLine($"[SYNTAX ERROR] {file}:");
                foreach (var diag in diagnostics)
                {
                    Console.WriteLine($"  {diag}");
                }
            }
        }

        if (errorCount == 0)
        {
            Console.WriteLine("Syntax check passed! Zero syntax errors found.");
        }
        else
        {
            Console.WriteLine($"Syntax check failed with {errorCount} errors.");
        }
    }

    static void RunCorrectnessTests()
    {
        var expander = new PathExpander();

        var testCases = new (string input, string expectedSubstring)[]
        {
            (@"C:\Windows\System32\drivers\etc\hosts", @"C:\Windows\System32\drivers\etc\hosts"),
            (@"%SystemDrive%", $"C:{Path.DirectorySeparatorChar}"),
            (@"%SystemDrive%\Users", @"C:\Users"),
            (@"%AppData%\TestApp", @"TestApp"),
            (@"%LocalAppData%\Google\Chrome", @"Google\Chrome"),
            (@"%ProgramFiles%\App", @"App"),
            (@"%UserProfile%\Documents", @"Documents"),
            (@"%Temp%\file.tmp", @"file.tmp"),
        };

        int passed = 0;
        foreach (var (input, expectedSubstring) in testCases)
        {
            var result = expander.ExpandVariables(input);
            if (result.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
            {
                passed++;
            }
            else
            {
                Console.WriteLine($"[FAIL] Input: '{input}', Expected substring: '{expectedSubstring}', Got: '{result}'");
            }
        }

        Console.WriteLine($"Correctness tests complete. Passed {passed}/{testCases.Length}.");
    }

    static void RunPerformanceBenchmark()
    {
        var expander = new PathExpander();
        var testPaths = new string[]
        {
            @"%LocalAppData%\Google\Chrome\User Data\Default\Cache",
            @"%AppData%\Mozilla\Firefox\Profiles",
            @"C:\Windows\System32\drivers\etc\hosts",
            @"%ProgramFiles%\Common Files\System",
            @"%SystemDrive%\Users\Public\Documents",
            @"D:\Games\Steam\steamapps",
            @"%Temp%\*.*",
            @"%WinDir%\Logs\CBS\CBS.log",
            @"%UserProfile%\Downloads\temp.txt",
            @"C:\Program Files (x86)\Microsoft\Edge\Application"
        };

        // Warmup
        for (int i = 0; i < 1000; i++)
            foreach (var p in testPaths) expander.ExpandVariables(p);

        int iterations = 100_000;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memBefore = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            foreach (var p in testPaths)
            {
                expander.ExpandVariables(p);
            }
        }
        sw.Stop();
        long memAfter = GC.GetTotalAllocatedBytes(true);

        Console.WriteLine($"Iterations: {iterations:N0} x {testPaths.Length} paths");
        Console.WriteLine($"Time: {sw.ElapsedMilliseconds} ms ({sw.ElapsedTicks} ticks)");
        Console.WriteLine($"Allocated: {(memAfter - memBefore) / 1024.0 / 1024.0:F2} MB");
    }
}
