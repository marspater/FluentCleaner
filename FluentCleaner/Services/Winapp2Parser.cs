using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // Optimized line enumeration over ReadOnlySpan<char> to avoid string allocations and string.Split
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line.Length >= 2 && line[0] == '[' && line[^1] == ']')
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

            // Zero-allocation key matching using ReadOnlySpan<char> comparison instead of Regex
            if      (key.Equals("LangSecRef",    StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(value, out var n)) current.LangSecRef = n; }
            else if (key.Equals("Section",       StringComparison.OrdinalIgnoreCase)) current.Section       = value.ToString();
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) current.SpecialDetect = value.ToString();
            else if (key.Equals("Warning",       StringComparison.OrdinalIgnoreCase)) current.Warning       = value.ToString();
            else if (key.Equals("Default",       StringComparison.OrdinalIgnoreCase)) current.Default       = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (IsDetectFileKey(key)) current.DetectFiles.Add(value.ToString());
            else if (IsDetectKey(key))     current.DetectKeys.Add(value.ToString());
            else if (IsFileKey(key))       current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (IsRegKey(key))        current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (IsExcludeKey(key))    current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    private static bool IsDetectFileKey(ReadOnlySpan<char> key) =>
        key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase) && IsDigits(key[10..]);

    private static bool IsDetectKey(ReadOnlySpan<char> key) =>
        key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase) && IsDigits(key[6..]);

    private static bool IsFileKey(ReadOnlySpan<char> key) =>
        key.Length > 7 && key.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase) && IsDigits(key[7..]);

    private static bool IsRegKey(ReadOnlySpan<char> key) =>
        key.Length > 6 && key.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase) && IsDigits(key[6..]);

    private static bool IsExcludeKey(ReadOnlySpan<char> key) =>
        key.Length > 10 && key.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase) && IsDigits(key[10..]);

    private static bool IsDigits(ReadOnlySpan<char> span)
    {
        foreach (var ch in span)
        {
            if (ch is < '0' or > '9') return false;
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
