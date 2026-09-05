using FluentCleaner.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public class Winapp2Parser
{
    public List<CleanerEntry> Parse(string content)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        foreach (var lineSpan in content.AsSpan().EnumerateLines())
        {
            var line = lineSpan.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                if (current is not null && IsValid(current)) entries.Add(current);

                var name = line[1..^1].Trim();
                if (name.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                current = new CleanerEntry { Name = name.ToString().TrimEnd('*').TrimEnd() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line[..eqIdx].Trim();
            var valueSpan = line[(eqIdx + 1)..].Trim();
            if (valueSpan.IsEmpty) continue;

            var keyStr = key.ToString();
            var value = valueSpan.ToString();

            if      (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(valueSpan, out var n)) current.LangSecRef = n; }
            else if (key.Equals("Section", StringComparison.OrdinalIgnoreCase)) current.Section = value;
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) current.SpecialDetect = value;
            else if (key.Equals("Warning", StringComparison.OrdinalIgnoreCase)) current.Warning = value;
            else if (key.Equals("Default", StringComparison.OrdinalIgnoreCase)) current.Default = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (IsDetectKey(key)) current.DetectKeys.Add(value);
            else if (IsDetectFileKey(key)) current.DetectFiles.Add(value);
            else if (IsFileKey(key)) current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (IsRegKey(key)) current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (IsExcludeKey(key)) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current)) entries.Add(current);
        return entries;
    }

    private static bool IsDetectKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = key[6..];
        foreach (var c in digits)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    private static bool IsDetectFileKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = key[10..];
        foreach (var c in digits)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    private static bool IsFileKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = key[7..];
        if (digits.IsEmpty) return false;
        foreach (var c in digits)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    private static bool IsRegKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = key[6..];
        if (digits.IsEmpty) return false;
        foreach (var c in digits)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    private static bool IsExcludeKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = key[10..];
        if (digits.IsEmpty) return false;
        foreach (var c in digits)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    // An entry is only useful if it can be detected AND has something to clean
    private static bool IsValid(CleanerEntry e) =>
        (e.DetectKeys.Count > 0 || e.DetectFiles.Count > 0 || e.SpecialDetect is not null) &&
        (e.FileKeys.Count  > 0  || e.RegKeys.Count  > 0);

    public async Task<List<CleanerEntry>> ParseFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return Parse(content);
    }
}
