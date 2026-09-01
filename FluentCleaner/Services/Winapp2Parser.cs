using FluentCleaner.Models;
using System.Text.RegularExpressions;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public class Winapp2Parser
{
    // High-performance INI parser using ReadOnlySpan<char> slicing without string splitting or Regex allocations
    public List<CleanerEntry> Parse(string content)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        ReadOnlySpan<char> span = content.AsSpan();
        int lineStart = 0;

        while (lineStart < span.Length)
        {
            int lineEnd = span[lineStart..].IndexOfAny('\r', '\n');
            int nextStart;
            ReadOnlySpan<char> rawLine;

            if (lineEnd < 0)
            {
                rawLine = span[lineStart..];
                nextStart = span.Length;
            }
            else
            {
                rawLine = span.Slice(lineStart, lineEnd);
                int actualEnd = lineStart + lineEnd;
                nextStart = actualEnd + 1;
                if (span[actualEnd] == '\r' && nextStart < span.Length && span[nextStart] == '\n')
                {
                    nextStart++;
                }
            }
            lineStart = nextStart;

            var line = rawLine.Trim();
            if (line.IsEmpty || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (current is not null && IsValid(current)) entries.Add(current);

                var nameSpan = line[1..^1].Trim();
                if (nameSpan.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    nameSpan.StartsWith("version", StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                while (nameSpan.Length > 0 && (nameSpan[^1] == '*' || char.IsWhiteSpace(nameSpan[^1])))
                {
                    nameSpan = nameSpan[..^1];
                }

                current = new CleanerEntry { Name = nameSpan.ToString() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.IsEmpty) continue;

            if (key.Equals("LangSecRef", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out var n)) current.LangSecRef = n;
            }
            else if (key.Equals("Section", StringComparison.OrdinalIgnoreCase)) current.Section = value.ToString();
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) current.SpecialDetect = value.ToString();
            else if (key.Equals("Warning", StringComparison.OrdinalIgnoreCase)) current.Warning = value.ToString();
            else if (key.Equals("Default", StringComparison.OrdinalIgnoreCase)) current.Default = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (IsKeyMatch(key, "DetectFile")) current.DetectFiles.Add(value.ToString());
            else if (IsKeyMatch(key, "Detect")) current.DetectKeys.Add(value.ToString());
            else if (IsKeyMatch(key, "FileKey", requireDigits: true)) current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (IsKeyMatch(key, "RegKey", requireDigits: true)) current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (IsKeyMatch(key, "ExcludeKey", requireDigits: true)) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
        }

        if (current is not null && IsValid(current)) entries.Add(current);
        return entries;
    }

    private static bool IsKeyMatch(ReadOnlySpan<char> key, string prefix, bool requireDigits = false)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = key[prefix.Length..];
        if (requireDigits && suffix.IsEmpty) return false;
        foreach (char c in suffix)
        {
            if (!char.IsAsciiDigit(c)) return false;
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
