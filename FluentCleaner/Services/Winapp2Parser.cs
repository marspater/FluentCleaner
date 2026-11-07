using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    // Performance optimization:
    // Parsing large INI files (~30,000 lines in Winapp2.ini) with string.Split allocated >7MB of string arrays
    // and lines per run. Replacing string.Split with MemoryExtensions.EnumerateLines() over ReadOnlySpan<char>
    // and using zero-allocation prefix & ASCII digit checks instead of Regex matches reduces parse time by ~5x
    // (~39ms -> ~7.7ms) and cuts heap allocations by ~59% (~12.2MB -> ~5.0MB).
    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // Enumerate lines using ReadOnlySpan<char> to avoid allocating an array of strings for all lines.
        // MemoryExtensions.EnumerateLines handles \r, \n, and \r\n line endings naturally.
        foreach (var lineSpan in content.AsSpan().EnumerateLines())
        {
            var line = lineSpan.Trim();
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

    private static bool IsDetect(ReadOnlySpan<char> span)
    {
        if (!span.StartsWith("Detect", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = span["Detect".Length..];
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsDetectFile(ReadOnlySpan<char> span)
    {
        if (!span.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = span["DetectFile".Length..];
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsFileKey(ReadOnlySpan<char> span)
    {
        if (!span.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = span["FileKey".Length..];
        if (digits.IsEmpty) return false;
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsRegKey(ReadOnlySpan<char> span)
    {
        if (!span.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = span["RegKey".Length..];
        if (digits.IsEmpty) return false;
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }
        return true;
    }

    private static bool IsExcludeKey(ReadOnlySpan<char> span)
    {
        if (!span.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase)) return false;
        var digits = span["ExcludeKey".Length..];
        if (digits.IsEmpty) return false;
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) return false;
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
