using FluentCleaner.Models;
using System.Text.RegularExpressions;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        // Pre-allocate capacity (~4000 entries in typical Winapp2.ini) to avoid dynamic resizes
        var entries = new List<CleanerEntry>(4096);
        CleanerEntry? current = null;

        // Use EnumerateLines over ReadOnlySpan<char> to avoid allocating string[] and line strings.
        // Handles \r, \n, and \r\n transparently.
        foreach (var rawLine in content.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line[0] == '[' && line[^1] == ']')
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

                // Strip the trailing " *" Winapp2 uses to mark community entries without string allocations
                while (!nameSpan.IsEmpty && (nameSpan[^1] == '*' || char.IsWhiteSpace(nameSpan[^1])))
                {
                    nameSpan = nameSpan[..^1];
                }

                current = new CleanerEntry { Name = nameSpan.ToString() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key   = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.IsEmpty) continue;

            // Fast branch dispatch based on first char of key
            switch (key[0])
            {
                case 'F':
                case 'f':
                    if (IsNumberedKey(key, "FileKey"))
                        current.FileKeys.Add(FileKeyEntry.Parse(value));
                    break;
                case 'R':
                case 'r':
                    if (IsNumberedKey(key, "RegKey"))
                        current.RegKeys.Add(RegKeyEntry.Parse(value));
                    break;
                case 'E':
                case 'e':
                    if (IsNumberedKey(key, "ExcludeKey"))
                        current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
                    break;
                case 'D':
                case 'd':
                    if (IsDetectFile(key))
                        current.DetectFiles.Add(value.ToString());
                    else if (IsDetect(key))
                        current.DetectKeys.Add(value.ToString());
                    else if (key.Equals("Default", StringComparison.OrdinalIgnoreCase))
                        current.Default = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    break;
                case 'L':
                case 'l':
                    if (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(value, out var n)) current.LangSecRef = n;
                    }
                    break;
                case 'S':
                case 's':
                    if (key.Equals("Section", StringComparison.OrdinalIgnoreCase))
                        current.Section = value.ToString();
                    else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase))
                        current.SpecialDetect = value.ToString();
                    break;
                case 'W':
                case 'w':
                    if (key.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                        current.Warning = value.ToString();
                    break;
            }
        }

        if (current is not null && IsValid(current, requireDetection)) entries.Add(current);
        return entries;
    }

    // Fast zero-allocation prefix and ASCII digit checks replacing compiled regexes
    private static bool IsDetect(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase)) return false;
        var rem = key[6..];
        if (rem.IsEmpty) return true;
        foreach (var ch in rem)
        {
            if (!char.IsAsciiDigit(ch)) return false;
        }
        return true;
    }

    private static bool IsDetectFile(ReadOnlySpan<char> key)
    {
        if (!key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase)) return false;
        var rem = key[10..];
        if (rem.IsEmpty) return true;
        foreach (var ch in rem)
        {
            if (!char.IsAsciiDigit(ch)) return false;
        }
        return true;
    }

    private static bool IsNumberedKey(ReadOnlySpan<char> key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var rem = key[prefix.Length..];
        if (rem.IsEmpty) return false;
        foreach (var ch in rem)
        {
            if (!char.IsAsciiDigit(ch)) return false;
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
