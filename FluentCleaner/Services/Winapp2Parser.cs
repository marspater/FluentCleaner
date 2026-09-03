using FluentCleaner.Models;
using System.Text.RegularExpressions;

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

        // Use ReadOnlySpan line enumeration to avoid allocating string[] or Regex for the whole file
        var span = content.AsSpan();
        while (span.Length > 0)
        {
            int lineEnd = span.IndexOfAny('\r', '\n');
            ReadOnlySpan<char> lineSpan;
            if (lineEnd >= 0)
            {
                lineSpan = span[..lineEnd];
                span = span[(lineEnd + 1)..];
                if (span.Length > 0 && lineSpan.Length > 0 && lineSpan[^1] == '\r' && span[0] == '\n')
                {
                    // Skip \n in \r\n
                }
            }
            else
            {
                lineSpan = span;
                span = ReadOnlySpan<char>.Empty;
            }

            lineSpan = lineSpan.Trim();
            if (lineSpan.IsEmpty || lineSpan[0] == ';' || lineSpan[0] == '#') continue;

            if (lineSpan[0] == '[' && lineSpan[^1] == ']')
            {
                if (current is not null && IsValid(current)) entries.Add(current);

                var nameSpan = lineSpan[1..^1].Trim();
                // Skip the file's own header block
                if (nameSpan.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    nameSpan.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                // Strip the trailing " *" Winapp2 uses to mark community entries
                var nameStr = nameSpan.ToString().TrimEnd('*').TrimEnd();
                current = new CleanerEntry { Name = nameStr };
                continue;
            }

            if (current is null) continue;

            var eqIdx = lineSpan.IndexOf('=');
            if (eqIdx < 0) continue;

            var keySpan   = lineSpan[..eqIdx].Trim();
            var valueSpan = lineSpan[(eqIdx + 1)..].Trim();
            if (valueSpan.IsEmpty) continue;

            if (keySpan.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(valueSpan, out var n)) current.LangSecRef = n;
            }
            else if (keySpan.Equals("Section", StringComparison.OrdinalIgnoreCase))
            {
                current.Section = valueSpan.ToString();
            }
            else if (keySpan.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase))
            {
                current.SpecialDetect = valueSpan.ToString();
            }
            else if (keySpan.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            {
                current.Warning = valueSpan.ToString();
            }
            else if (keySpan.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                current.Default = valueSpan.Equals("True", StringComparison.OrdinalIgnoreCase);
            }
            else if (IsDetectFileKey(keySpan))
            {
                current.DetectFiles.Add(valueSpan.ToString());
            }
            else if (IsDetectKey(keySpan))
            {
                current.DetectKeys.Add(valueSpan.ToString());
            }
            else if (IsFileKey(keySpan))
            {
                current.FileKeys.Add(FileKeyEntry.Parse(valueSpan));
            }
            else if (IsRegKey(keySpan))
            {
                current.RegKeys.Add(RegKeyEntry.Parse(valueSpan));
            }
            else if (IsExcludeKey(keySpan))
            {
                current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(valueSpan));
            }
        }

        if (current is not null && IsValid(current)) entries.Add(current);
        return entries;
    }

    private static bool IsDetectFileKey(ReadOnlySpan<char> key)
    {
        // Matches ^DetectFile\d*$
        if (key.StartsWith("DetectFile", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllDigits(key[10..]);
        }
        return false;
    }

    private static bool IsDetectKey(ReadOnlySpan<char> key)
    {
        // Matches ^Detect\d*$"
        if (key.StartsWith("Detect", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllDigits(key[6..]);
        }
        return false;
    }

    private static bool IsFileKey(ReadOnlySpan<char> key)
    {
        // Matches ^FileKey\d+$ (at least 1 digit required)
        if (key.Length > 7 && key.StartsWith("FileKey", StringComparison.OrdinalIgnoreCase))
        {
            var digits = key[7..];
            return !digits.IsEmpty && IsAllDigits(digits);
        }
        return false;
    }

    private static bool IsRegKey(ReadOnlySpan<char> key)
    {
        // Matches ^RegKey\d+$ (at least 1 digit required)
        if (key.Length > 6 && key.StartsWith("RegKey", StringComparison.OrdinalIgnoreCase))
        {
            var digits = key[6..];
            return !digits.IsEmpty && IsAllDigits(digits);
        }
        return false;
    }

    private static bool IsExcludeKey(ReadOnlySpan<char> key)
    {
        // Matches ^ExcludeKey\d+$ (at least 1 digit required)
        if (key.Length > 10 && key.StartsWith("ExcludeKey", StringComparison.OrdinalIgnoreCase))
        {
            var digits = key[10..];
            return !digits.IsEmpty && IsAllDigits(digits);
        }
        return false;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> span)
    {
        foreach (char c in span)
        {
            if (c < '0' || c > '9') return false;
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
