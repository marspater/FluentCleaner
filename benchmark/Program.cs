using System;
using System.Diagnostics;
using System.IO;
using FluentCleaner.Services;

class Program
{
    static void Main()
    {
        if (!File.Exists("Winapp2.ini"))
        {
            Console.WriteLine("Winapp2.ini not found.");
            return;
        }

        var content = File.ReadAllText("Winapp2.ini");
        var parser = new Winapp2Parser();

        // Warmup
        for (int i = 0; i < 5; i++)
        {
            var warmupEntries = parser.Parse(content);
        }

        var sw = Stopwatch.StartNew();
        int iterations = 100;
        int count = 0;
        for (int i = 0; i < iterations; i++)
        {
            var entries = parser.Parse(content);
            count = entries.Count;
        }
        sw.Stop();

        Console.WriteLine($"Parsed Winapp2.ini {iterations} times in {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / (double)iterations:F2} ms/op). Entries parsed: {count}");
    }
}
