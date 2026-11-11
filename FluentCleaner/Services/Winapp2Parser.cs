using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    // Performance optimization: Enumerate lines as ReadOnlySpan<char> and use zero-allocation
    // span prefix/digit checks instead of string.Split() array allocations and Regex matching.
    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        foreach (var rawLine in MemoryExtensions.EnumerateLines(content.AsSpan()))
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (current is not null && IsValid(current, requireDetection)) entries.Add(current);

                var nameSpan = line[1..^1].Trim();
                // Skip the file's own header block
                if (nameSpan.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    nameSpan.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                // Strip the trailing " *" Winapp2 uses to mark community entries
                current = new CleanerEntry { Name = nameSpan.TrimEnd('*').TrimEnd().ToString() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key   = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.IsEmpty) continue;

            if      (key.Equals("LangSecRef",    StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(value, out var n)) current.LangSecRef = n; }
            else if (key.Equals("Section",       StringComparison.OrdinalIgnoreCase)) current.Section       = value.ToString();
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) current.SpecialDetect = value.ToString();
            else if (key.Equals("Warning",       StringComparison.OrdinalIgnoreCase)) current.Warning       = value.ToString();
            else if (key.Equals("Default",       StringComparison.OrdinalIgnoreCase)) current.Default       = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (IsDetectFile(key)) current.DetectFiles.Add(value.ToString());
            else if (IsDetect(key))     current.DetectKeys.Add(value.ToString());
            else if (IsFileKey(key))    current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (IsRegKey(key))     current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (IsExcludeKey(key)) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return false;
        foreach (var c in span)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsDetect(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key["Detect".Length..];
        if (suffix.IsEmpty) return true;
        foreach (var c in suffix)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsDetectFile(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key["DetectFile".Length..];
        if (suffix.IsEmpty) return true;
        foreach (var c in suffix)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsFileKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase)) return false;
        return IsAllDigits(key["FileKey".Length..]);
    }

    private static bool IsRegKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase)) return false;
        return IsAllDigits(key["RegKey".Length..]);
    }

    private static bool IsExcludeKey(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase)) return false;
        return IsAllDigits(key["ExcludeKey".Length..]);
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
