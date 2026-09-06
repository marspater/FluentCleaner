using System.Collections.Concurrent;
using FluentCleaner.Models;

namespace FluentCleaner.Services;

// Answers the question: "is this app even installed?"
// Multiple Detect/DetectFile entries use OR logic; one hit is enough.
public class DetectionService(PathExpander? expander = null)
{
    private readonly PathExpander _expander = expander ?? new();
    private static readonly ConcurrentDictionary<string, bool> _regCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> _fileCache = new(StringComparer.OrdinalIgnoreCase);

    public static void ClearCache()
    {
        _regCache.Clear();
        _fileCache.Clear();
    }

    public bool IsInstalled(CleanerEntry entry)
    {
        if (entry.SpecialDetect is not null)
        {
            if (TryCheckSpecialDetect(entry.SpecialDetect, out bool result))
                return result;
        }

        foreach (var reg  in entry.DetectKeys)  if (CheckRegistryCached(reg))  return true;
        foreach (var file in entry.DetectFiles) if (CheckFileCached(file))     return true;

        return false;
    }

    private static bool CheckRegistryCached(string regPath) =>
        _regCache.GetOrAdd(regPath, CheckRegistry);

    private bool CheckFileCached(string rawPath) =>
        _fileCache.GetOrAdd(rawPath, CheckFile);

    private static bool CheckRegistry(string regPath)
    {
        try
        {
            var (hive, subKey, valueName) = RegistryHelper.SplitRegPath(regPath);
            using var key = RegistryHelper.OpenKey(hive, subKey);
            if (key is null) return false;
            return valueName is null || key.GetValue(valueName) is not null;
        }
        catch { return false; }
    }

    private bool CheckFile(string rawPath)
    {
        try
        {
            var expanded = _expander.ExpandVariables(rawPath);
            if (expanded.Contains('*') || expanded.Contains('?'))
                return _expander.ResolvePaths(rawPath).Count > 0;
            return File.Exists(expanded) || Directory.Exists(expanded);
        }
        catch { return false; }
    }

    private bool TryCheckSpecialDetect(string code, out bool result)
    {
        switch (code.ToUpperInvariant())
        {
            case "DET_CHROME":
                result = CheckFileCached(@"%LocalAppData%\Google\Chrome\User Data"); return true;
            case "DET_FIREFOX":
                result = CheckFileCached(@"%AppData%\Mozilla\Firefox"); return true;
            case "DET_IE":
                result = CheckRegistryCached(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\IEXPLORE.EXE"); return true;
            case "DET_THUNDERBIRD":
                result = CheckFileCached(@"%AppData%\Thunderbird"); return true;
            case "DET_OPERA":
                result = CheckFileCached(@"%AppData%\Opera Software\Opera Stable"); return true;
            case "DET_EDGE":
                result = CheckFileCached(@"%LocalAppData%\Microsoft\Edge\User Data"); return true;
            case "DET_WINSTORE":
                result = CheckFileCached(@"%LocalAppData%\Packages"); return true;
            default:
                result = false; return false;
        }
    }
}
