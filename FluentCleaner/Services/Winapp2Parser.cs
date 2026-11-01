using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
// Optimized with ReadOnlySpan<char>.EnumerateLines() and direct prefix/digit matching
// to avoid string splitting and regex allocation overhead on 30,000+ line INI files.
public partial class Winapp2Parser
{
    public List<CleanerEntry> Parse(string content, bool requireDetection = true) =>
        Parse(content.AsSpan(), requireDetection);

    public List<CleanerEntry> Parse(ReadOnlySpan<char> content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // EnumerateLines splits on \r, \n, and \r\n without allocating string arrays
        foreach (var rawLine in content.EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

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
            if (value.IsEmpty) continue;

            if      (key.Equals("LangSecRef",    StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(value, out var n)) current.LangSecRef = n; }
            else if (key.Equals("Section",       StringComparison.OrdinalIgnoreCase)) current.Section       = value.ToString();
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) current.SpecialDetect = value.ToString();
            else if (key.Equals("Warning",       StringComparison.OrdinalIgnoreCase)) current.Warning       = value.ToString();
            else if (key.Equals("Default",       StringComparison.OrdinalIgnoreCase)) current.Default       = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (IsDetect(key))     current.DetectKeys.Add(value.ToString());
            else if (IsDetectFile(key)) current.DetectFiles.Add(value.ToString());
            else if (IsFileKey(key))    current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (IsRegKey(key))     current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (IsExcludeKey(key)) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    private static bool IsAllAsciiDigits(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return false;
        foreach (var c in span)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsAllAsciiDigitsOrEmpty(ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    // Matches ^FileKey\d+$
    private static bool IsFileKey(ReadOnlySpan<char> key) =>
        key.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase) &&
        IsAllAsciiDigits(key[7..]);

    // Matches ^RegKey\d+$
    private static bool IsRegKey(ReadOnlySpan<char> key) =>
        key.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase) &&
        IsAllAsciiDigits(key[6..]);

    // Matches ^ExcludeKey\d+$
    private static bool IsExcludeKey(ReadOnlySpan<char> key) =>
        key.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase) &&
        IsAllAsciiDigits(key[10..]);

    // Matches ^DetectFile\d*$
    private static bool IsDetectFile(ReadOnlySpan<char> key) =>
        key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase) &&
        IsAllAsciiDigitsOrEmpty(key[10..]);

    // Matches ^Detect\d*$ (excludes DetectFile)
    private static bool IsDetect(ReadOnlySpan<char> key) =>
        key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) &&
        !key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase) &&
        IsAllAsciiDigitsOrEmpty(key[6..]);

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
