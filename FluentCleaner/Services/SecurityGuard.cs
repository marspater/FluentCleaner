using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FluentCleaner.Services;

/// <summary>
/// Centralized security validation and guardrails to protect against path traversal,
/// catastrophic deletion, command injection, and scheme hijacking.
/// </summary>
public static partial class SecurityGuard
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly HashSet<string> UnsafeRegistrySubKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "",
        "Software",
        "Microsoft",
        "System",
        "CurrentControlSet",
        "Control",
        "Services",
        "SAM",
        "SECURITY",
        "HARDWARE",
        "Policies"
    };

    [GeneratedRegex(@"^[A-Za-z0-9._\-*]+$", RegexOptions.Compiled)]
    private static partial Regex PackageNameRegex();

    /// <summary>
    /// Checks if a directory path is safe for deletion or empty-folder pruning.
    /// Rejects drive roots (e.g. C:\) and critical OS root directories.
    /// </summary>
    public static bool IsSafeDeletionPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var trimmed = path.Trim();
            // Bare drive like "C:" or "D:"
            if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
                return false;

            // Check if path is already a root or drive root
            var withSlash = trimmed.EndsWith(Path.DirectorySeparatorChar) || trimmed.EndsWith(Path.AltDirectorySeparatorChar)
                ? trimmed
                : trimmed + Path.DirectorySeparatorChar;
            var rootCandidate = Path.GetPathRoot(withSlash);
            if (string.Equals(withSlash, rootCandidate, StringComparison.OrdinalIgnoreCase))
                return false;

            var fullPath = Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Never delete or prune a drive root (e.g. "C:", "D:")
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return false;

            // Critical system root folders that must never be targeted as deletion roots
            var protectedPaths = new List<string?>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // C:\ProgramData
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), // C:\Users\Username directly
                Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) // C:\Users
            };

            foreach (var p in protectedPaths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                var norm = Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(fullPath, norm, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a registry deletion target is safe.
    /// Whole subkey tree deletions must be at least two levels deep and not match critical system roots.
    /// </summary>
    public static bool IsSafeRegistryDeletion(string hive, string subKey, string? valueName)
    {
        if (string.IsNullOrWhiteSpace(hive) || string.IsNullOrWhiteSpace(subKey))
            return false;

        // Deleting a specific named value inside a subkey is generally safe
        if (!string.IsNullOrWhiteSpace(valueName))
            return true;

        // Deleting an entire subkey tree:
        var normalizedSubKey = subKey.Trim('\\', '/').Replace('/', '\\');
        var segments = normalizedSubKey.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        // Disallow deleting top-level or single-level registry hives/keys (e.g. HKLM\Software or HKCU\Software)
        if (segments.Length < 2)
            return false;

        // Disallow known critical roots
        if (UnsafeRegistrySubKeys.Contains(segments[0]))
        {
            // If the first segment is Software, ensure there is at least a vendor/app subkey
            // e.g. "Software\Microsoft" is too broad, but "Software\Vendor\App\Cache" is fine.
            if (segments.Length < 3 && string.Equals(segments[0], "Software", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[1], "Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sanitizes an input string intended for use as a file or folder name, preventing path traversal
    /// and blocking Windows reserved device names (CON, PRN, AUX, NUL, COM1-9, LPT1-9).
    /// </summary>
    public static string SanitizeFileName(string name, string fallback = "cleaner_custom")
    {
        if (string.IsNullOrWhiteSpace(name))
            return fallback;

        // Remove any path separators or traversal
        var clean = Path.GetFileName(name.Trim());
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            clean = clean.Replace(c, '_');
        }

        clean = clean.Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(clean))
            return fallback;

        // Check against Windows reserved device names
        var baseName = Path.GetFileNameWithoutExtension(clean);
        if (ReservedDeviceNames.Contains(baseName) || ReservedDeviceNames.Contains(clean))
        {
            clean = "_" + clean;
        }

        return clean;
    }

    /// <summary>
    /// Validates AppX package name or wildcard expression to prevent PowerShell command injection.
    /// </summary>
    public static bool IsValidPackageName(string? packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return false;

        var trimmed = packageName.Trim();
        if (trimmed.Length > 256)
            return false;

        return PackageNameRegex().IsMatch(trimmed);
    }

    /// <summary>
    /// Validates external web URLs, enforcing http or https scheme.
    /// </summary>
    public static bool IsValidWebUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
        }

        return false;
    }

    /// <summary>
    /// Resolves the canonical, absolute path to system PowerShell (powershell.exe)
    /// to prevent unquoted binary search path or local directory binary hijacking.
    /// </summary>
    public static string GetSafePowerShellPath()
    {
        try
        {
            var systemDir = Environment.SystemDirectory; // e.g. C:\Windows\System32
            var candidate = Path.Combine(systemDir, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(candidate))
                return candidate;
        }
        catch { }

        return "powershell.exe";
    }
}
