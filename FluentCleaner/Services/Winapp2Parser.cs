using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    // High-performance key format validation routines operating directly on ReadOnlySpan<char>.
    // Replaces Regex.IsMatch / allocations with zero-allocation span checks.
    private static bool StartsWithDigits(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return false;
        foreach (char c in span)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool StartsWithOptionalDigits(ReadOnlySpan<char> span)
    {
        foreach (char c in span)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // Optimization: Use MemoryExtensions.EnumerateLines over ReadOnlySpan<char> instead of
        // content.Split(['\r', '\n']). This avoids allocating a 30,000+ element string array and
        // eliminates intermediate line string allocations during parsing.
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
            else if (key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase) && StartsWithOptionalDigits(key["DetectFile".Length..]))
            {
                current.DetectFiles.Add(value.ToString());
            }
            else if (key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) && StartsWithOptionalDigits(key["Detect".Length..]))
            {
                current.DetectKeys.Add(value.ToString());
            }
            else if (key.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase) && StartsWithDigits(key["FileKey".Length..]))
            {
                current.FileKeys.Add(FileKeyEntry.Parse(value));
            }
            else if (key.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase) && StartsWithDigits(key["RegKey".Length..]))
            {
                current.RegKeys.Add(RegKeyEntry.Parse(value));
            }
            else if (key.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase) && StartsWithDigits(key["ExcludeKey".Length..]))
            {
                current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
            }
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
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
