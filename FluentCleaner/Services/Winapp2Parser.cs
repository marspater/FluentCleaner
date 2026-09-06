using FluentCleaner.Models;
using System.Text.RegularExpressions;

namespace FluentCleaner.Services;

// Parses the Winapp2.ini format into CleanerEntry objects.
// The format is INI-like but with numbered multi-value keys:
// FileKey1=..., FileKey2=..., Detect, Detect1, Detect2, etc.
public partial class Winapp2Parser
{
    [GeneratedRegex(@"^FileKey\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex RxFileKey();

    [GeneratedRegex(@"^RegKey\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex RxRegKey();

    [GeneratedRegex(@"^ExcludeKey\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex RxExcludeKey();

    [GeneratedRegex(@"^Detect\d*$", RegexOptions.IgnoreCase)]
    private static partial Regex RxDetect();

    [GeneratedRegex(@"^DetectFile\d*$", RegexOptions.IgnoreCase)]
    private static partial Regex RxDetectFile();

    public List<CleanerEntry> Parse(string content, bool requireDetection = true)
    {
        var entries = new List<CleanerEntry>();
        CleanerEntry? current = null;

        //Split on both \r and \n;WinUI 3 TextBox saves with \r only (not \r\n),
        //so splitting on just \n would leave the entire file as a single line
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (current is not null && IsValid(current, requireDetection)) entries.Add(current);

                var name = line[1..^1].Trim();
                //Skip the files own header block
                if (name.StartsWith("Winapp2", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("version",  StringComparison.OrdinalIgnoreCase))
                {
                    current = null;
                    continue;
                }

                //Strip the trailing " *" Winapp2 uses to mark community entries
                current = new CleanerEntry { Name = name.TrimEnd('*').TrimEnd() };
                continue;
            }

            if (current is null) continue;

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key   = line[..eqIdx].Trim();
            var value = line[(eqIdx + 1)..].Trim();
            if (value.Length == 0) continue;

            if      (key.Equals("LangSecRef",    StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(value, out var n)) current.LangSecRef = n; }
            else if (key.Equals("Section",       StringComparison.OrdinalIgnoreCase)) current.Section       = value;
            else if (key.Equals("SpecialDetect", StringComparison.OrdinalIgnoreCase)) current.SpecialDetect = value;
            else if (key.Equals("Warning",       StringComparison.OrdinalIgnoreCase)) current.Warning       = value;
            else if (key.Equals("Default",       StringComparison.OrdinalIgnoreCase)) current.Default       = value.Equals("True", StringComparison.OrdinalIgnoreCase);
            else if (RxDetect().IsMatch(key))     current.DetectKeys.Add(value);
            else if (RxDetectFile().IsMatch(key)) current.DetectFiles.Add(value);
            else if (RxFileKey().IsMatch(key))    current.FileKeys.Add(FileKeyEntry.Parse(value));
            else if (RxRegKey().IsMatch(key))     current.RegKeys.Add(RegKeyEntry.Parse(value));
            else if (RxExcludeKey().IsMatch(key)) current.ExcludeKeys.Add(ExcludeKeyEntry.Parse(value));
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
