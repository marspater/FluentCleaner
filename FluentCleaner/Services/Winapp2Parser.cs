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

        // Bolt optimization: Use ReadOnlySpan<char> line enumeration to eliminate string[] array allocations.
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
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
            else if (IsMatchKey(key, "Detect", requireDigits: false))
            {
                current.DetectKeys.Add(value.ToString());
            }
            else if (IsMatchKey(key, "DetectFile", requireDigits: false))
            {
                current.DetectFiles.Add(value.ToString());
            }
            else if (IsMatchKey(key, "FileKey", requireDigits: true))
            {
                current.FileKeys.Add(FileKeyEntry.Parse(value));
            }
            else if (IsMatchKey(key, "RegKey", requireDigits: true))
            {
                current.RegKeys.Add(RegKeyEntry.Parse(value));
            }
            else if (IsMatchKey(key, "ExcludeKey", requireDigits: true))
            {
                current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
            }
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    // Bolt optimization: Fast zero-allocation replacement for Regex matching on key names.
    // Checks that key starts with prefix and remaining characters are ASCII digits.
    private static bool IsMatchKey(ReadOnlySpan<char> key, ReadOnlySpan<char> prefix, bool requireDigits = true)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key[prefix.Length..];
        if (requireDigits && suffix.IsEmpty) return false;
        foreach (var c in suffix)
            if (!char.IsAsciiDigit(c)) return false;
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
