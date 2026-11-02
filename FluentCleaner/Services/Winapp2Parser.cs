using FluentCleaner.Models;
using System.Text.RegularExpressions;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public class Winapp2Parser
{
    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // MemoryExtensions.EnumerateLines() handles \r, \n, and \r\n without allocating string arrays
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                if (current is not null && IsValid(current, requireDetection)) entries.Add(current);

                var name = line[1..^1].Trim();
                // Skip the file's own header block
                if (name.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                // Strip the trailing " *" Winapp2 uses to mark community entries
                current = new CleanerEntry { Name = name.TrimEnd('*').TrimEnd().ToString() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key   = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.Length == 0) continue;

            if (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out var n)) current.LangSecRef = n;
            }
            else if (key.Equals("Section", StringComparison.OrdinalIgnoreCase))
            {
                current.Section = value.ToString();
            }
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase))
            {
                current.SpecialDetect = value.ToString();
            }
            else if (key.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            {
                current.Warning = value.ToString();
            }
            else if (key.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                current.Default = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            }
            else if (IsKeyMatch(key, "DetectFile"))
            {
                current.DetectFiles.Add(value.ToString());
            }
            else if (IsKeyMatch(key, "Detect"))
            {
                current.DetectKeys.Add(value.ToString());
            }
            else if (IsKeyMatchWithDigits(key, "FileKey"))
            {
                current.FileKeys.Add(FileKeyEntry.Parse(value));
            }
            else if (IsKeyMatchWithDigits(key, "RegKey"))
            {
                current.RegKeys.Add(RegKeyEntry.Parse(value));
            }
            else if (IsKeyMatchWithDigits(key, "ExcludeKey"))
            {
                current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
            }
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    // Matches Detect, Detect1, DetectFile, DetectFile1 etc. (prefix + optional ASCII digits)
    private static bool IsKeyMatch(ReadOnlySpan<char> key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var digits = key[prefix.Length..];
        for (int i = 0; i < digits.Length; i++)
        {
            if (!char.IsAsciiDigit(digits[i])) return false;
        }
        return true;
    }

    // Matches FileKey1, RegKey2, ExcludeKey3 etc. (prefix + at least one ASCII digit)
    private static bool IsKeyMatchWithDigits(ReadOnlySpan<char> key, string prefix)
    {
        if (key.Length <= prefix.Length || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var digits = key[prefix.Length..];
        for (int i = 0; i < digits.Length; i++)
        {
            if (!char.IsAsciiDigit(digits[i])) return false;
        }
        return true;
    }

    // An entry is only useful if it can be detected (unless detection is optional) AND has something to clean
    private static bool IsValid(CleanerEntry e, bool requireDetection) =>
        (!requireDetection || e.DetectKeys.Count > 0 || e.DetectFiles.Count > 0 || e.SpecialDetect is not null) &&
        (e.FileKeys.Count  > 0  || e.RegKeys.Count  > 0);

    public async Task<List<CleanerEntry>> ParseFileAsync(string filePath, bool requireDetection = true)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return Parse(content, requireDetection);
    }
}
