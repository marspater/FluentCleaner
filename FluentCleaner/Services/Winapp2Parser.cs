using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public class Winapp2Parser
{
    public List<CleanerEntry> Parse(string content)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        // Optimized allocation-free line enumeration using Span and EnumerateLines
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                if (current is not null && IsValid(current)) entries.Add(current);

                var nameSpan = line[1..^1].Trim();
                // Skip the file's own header block
                if (nameSpan.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    nameSpan.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                // Strip trailing asterisk/whitespace used to mark community entries
                var name = nameSpan.TrimEnd('*').TrimEnd().ToString();
                current = new CleanerEntry { Name = name };
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
            // Fast Span-based key matching replacing Regex.IsMatch allocations & CPU overhead
            else if (IsKey(key, "Detect"))          current.DetectKeys.Add(value.ToString());
            else if (IsKey(key, "DetectFile"))      current.DetectFiles.Add(value.ToString());
            else if (IsKeyWithDigits(key, "FileKey"))    current.FileKeys.Add(FileKeyEntry.Parse(value.ToString()));
            else if (IsKeyWithDigits(key, "RegKey"))     current.RegKeys.Add(RegKeyEntry.Parse(value.ToString()));
            else if (IsKeyWithDigits(key, "ExcludeKey")) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value.ToString()));
        }

        if (current is not null && IsValid(current)) entries.Add(current);
        return entries;
    }

    // Matches keys with optional trailing ASCII digits (e.g., Detect or Detect1)
    private static bool IsKey(ReadOnlySpan<char> key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key[prefix.Length..];
        for (int i = 0; i < suffix.Length; i++)
        {
            if (!char.IsAsciiDigit(suffix[i])) return false;
        }
        return true;
    }

    // Matches keys with required trailing ASCII digits (e.g., FileKey1, RegKey2, ExcludeKey1)
    private static bool IsKeyWithDigits(ReadOnlySpan<char> key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key[prefix.Length..];
        if (suffix.Length == 0) return false;
        for (int i = 0; i < suffix.Length; i++)
        {
            if (!char.IsAsciiDigit(suffix[i])) return false;
        }
        return true;
    }

    // An entry is only useful if it can be detected AND has something to clean
    private static bool IsValid(CleanerEntry e) =>
        (e.DetectKeys.Count > 0 || e.DetectFiles.Count > 0 || e.SpecialDetect is not null) &&
        (e.FileKeys.Count  > 0  || e.RegKeys.Count  > 0);

    public async Task<List<CleanerEntry>> ParseFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return Parse(content);
    }
}
