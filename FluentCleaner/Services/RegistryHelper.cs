using Microsoft.Win32;

namespace FluentCleaner.Services;

public static class RegistryHelper
{
    // Maps registry hive abbreviations to Microsoft.Win32.Registry root keys.
    public static RegistryKey? OpenHive(string hive) => hive switch
    {
        "HKCU" or "HKEY_CURRENT_USER"   => Registry.CurrentUser,
        "HKLM" or "HKEY_LOCAL_MACHINE"  => Registry.LocalMachine,
        "HKU"  or "HKEY_USERS"          => Registry.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
        "HKCR" or "HKEY_CLASSES_ROOT"   => Registry.ClassesRoot,
        _ => null
    };

    // Splits a path like "HKCU\Software\App" into ("HKCU", "Software\App").
    public static (string hive, string subKey) SplitHiveSubKey(string path)
    {
        var idx = path.IndexOf('\\');
        return idx < 0 ? (path.ToUpperInvariant(), "") : (path[..idx].ToUpperInvariant(), path[(idx + 1)..]);
    }

    // Splits a full registry key/value string like "HKCU\Software\App|ValueName"
    // into ("HKCU", "Software\App", "ValueName").
    public static (string hive, string subKey, string? valueName) SplitRegPath(string path)
    {
        string regPath = path;
        string? valueName = null;

        var pipeIdx = path.LastIndexOf('|');
        if (pipeIdx >= 0)
        {
            regPath = path[..pipeIdx];
            valueName = path[(pipeIdx + 1)..];
        }

        var (hive, subKey) = SplitHiveSubKey(regPath);
        return (hive, subKey, valueName);
    }

    // Opens a registry key safely. Returns null if hive or key doesn't exist or access is denied.
    public static RegistryKey? OpenKey(string hive, string subKey, bool writable = false)
    {
        try
        {
            return OpenHive(hive)?.OpenSubKey(subKey, writable);
        }
        catch
        {
            return null;
        }
    }
}
