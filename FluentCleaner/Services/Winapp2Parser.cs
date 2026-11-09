using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public class Winapp2Parser
{
    private static bool IsAllAsciiDigits(ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if ((uint)(span[i] - '0') > 9) return false;
        }
        return true;
    }

    // Matches ^FileKey\d+$ without regex allocation overhead
    private static bool IsFileKey(ReadOnlySpan<char> key)
    {
        const string prefix = "FileKey";
        return key.Length > prefix.Length &&
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               IsAllAsciiDigits(key.Slice(prefix.Length));
    }

    // Matches ^RegKey\d+$ without regex allocation overhead
    private static bool IsRegKey(ReadOnlySpan<char> key)
    {
        const string prefix = "RegKey";
        return key.Length > prefix.Length &&
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               IsAllAsciiDigits(key.Slice(prefix.Length));
    }

    // Matches ^ExcludeKey\d+$ without regex allocation overhead
    private static bool IsExcludeKey(ReadOnlySpan<char> key)
    {
        const string prefix = "ExcludeKey";
        return key.Length > prefix.Length &&
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               IsAllAsciiDigits(key.Slice(prefix.Length));
    }

    // Matches ^Detect\d*$ without regex allocation overhead
    private static bool IsDetect(ReadOnlySpan<char> key)
    {
        const string prefix = "Detect";
        return key.Length >= prefix.Length &&
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               IsAllAsciiDigits(key.Slice(prefix.Length));
    }

    // Matches ^DetectFile\d*$ without regex allocation overhead
    private static bool IsDetectFile(ReadOnlySpan<char> key)
    {
        const string prefix = "DetectFile";
        return key.Length >= prefix.Length &&
               key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               IsAllAsciiDigits(key.Slice(prefix.Length));
    }

    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // MemoryExtensions.EnumerateLines handles \r, \n, and \r\n without allocating string arrays
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (current is not null && IsValid(current, requireDetection)) entries.Add(current);

                var nameSpan = line.Slice(1, line.Length - 2).Trim();
                // Skip the file's own header block
                if (nameSpan.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    nameSpan.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                // Strip the trailing " *" Winapp2 uses to mark community entries
                nameSpan = nameSpan.TrimEnd('*').TrimEnd();
                current = new CleanerEntry { Name = nameSpan.ToString() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key   = line.Slice(0, eqIdx).Trim();
            var value = line.Slice(eqIdx + 1).Trim();
            if (value.IsEmpty) continue;

            if (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out var n)) current.LangSecRef = n;
            }
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
