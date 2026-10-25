using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    // Performance Optimization:
    // Parsing large INI files (~1.4MB, 30,000+ lines) using content.Split allocates over 7MB of string arrays.
    // MemoryExtensions.EnumerateLines provides zero-allocation ReadOnlySpan<char> line iteration.
    // KeyMatchesPrefixAndDigits replaces compiled Regex calls with zero-allocation span prefix and digit checks.
    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        foreach (var rawLine in content.AsSpan().EnumerateLines())
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
            else if (KeyMatchesPrefixAndDigits(key, "DetectFile", requireDigits: false)) current.DetectFiles.Add(value.ToString());
            else if (KeyMatchesPrefixAndDigits(key, "Detect", requireDigits: false))     current.DetectKeys.Add(value.ToString());
            else if (KeyMatchesPrefixAndDigits(key, "FileKey", requireDigits: true))     current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (KeyMatchesPrefixAndDigits(key, "RegKey", requireDigits: true))      current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (KeyMatchesPrefixAndDigits(key, "ExcludeKey", requireDigits: true))  current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    // Checks whether 'key' starts with 'prefix' (case-insensitive) and the remaining suffix consists solely of ASCII digits.
    // If 'requireDigits' is true, at least 1 digit must be present (e.g. FileKey1).
    // If 'requireDigits' is false, 0 or more digits are allowed (e.g. Detect / Detect1).
    private static bool KeyMatchesPrefixAndDigits(ReadOnlySpan<char> key, string prefix, bool requireDigits)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = key[prefix.Length..];
        if (requireDigits && remainder.IsEmpty)
            return false;

        foreach (var c in remainder)
        {
            if (!char.IsAsciiDigit(c))
                return false;
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
