using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    // High-performance parser: uses ReadOnlySpan<char> line enumeration and prefix matching
    // to avoid allocating string arrays or running regex routines over ~30,000 lines.
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
                    nameSpan.StartsWith("version", StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                // Strip the trailing " *" Winapp2 uses to mark community entries
                var trimmedName = nameSpan.TrimEnd();
                while (trimmedName.EndsWith('*'))
                {
                    trimmedName = trimmedName[..^1].TrimEnd();
                }

                current = new CleanerEntry { Name = trimmedName.ToString() };
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
            else if (IsKeyMatch(key, "DetectFile", allowNoDigits: true))  current.DetectFiles.Add(value.ToString());
            else if (IsKeyMatch(key, "Detect",     allowNoDigits: true))  current.DetectKeys.Add(value.ToString());
            else if (IsKeyMatch(key, "FileKey",    allowNoDigits: false)) current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (IsKeyMatch(key, "RegKey",     allowNoDigits: false)) current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (IsKeyMatch(key, "ExcludeKey", allowNoDigits: false)) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    // Zero-allocation check to match keys like Detect, Detect1, FileKey1, RegKey2, etc.
    private static bool IsKeyMatch(ReadOnlySpan<char> key, ReadOnlySpan<char> prefix, bool allowNoDigits)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = key[prefix.Length..];
        if (remainder.IsEmpty)
            return allowNoDigits;

        foreach (char c in remainder)
        {
            if (c < '0' || c > '9')
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
