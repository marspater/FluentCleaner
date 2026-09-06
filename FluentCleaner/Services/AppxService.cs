using System.Diagnostics;

namespace FluentCleaner.Services;

// One entry from Winappx.ini
public record AppxEntry(string Name, string PackageName, string? Warning);

//Standalone PowerShell-backed service for AppX debloating.
// No dependency on CleanerEntry, ScanResult, or any other cleaner infrastructure
public static class AppxService
{
    // --- Parse Winappx.ini -------------------------------------------------------

    // Reads all [Section] blocks and maps them to AppxEntry records.
    // Lines starting with ';' and blank lines are ignored.
    public static async Task<List<AppxEntry>> ParseDatabaseAsync(string path)
    {
        var entries = new List<AppxEntry>();
        string? name = null, packageName = null, warning = null;

        foreach (var raw in await File.ReadAllLinesAsync(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                // Flush the previous block before starting a new one
                if (name is not null && packageName is not null)
                    entries.Add(new AppxEntry(name, packageName, warning));

                // Strip trailing " *" that winapp2-style names sometimes use
                name        = line[1..^1].TrimEnd('*', ' ').Trim();
                packageName = null;
                warning     = null;
            }
            else if (line.StartsWith("PackageName=", StringComparison.OrdinalIgnoreCase))
                packageName = line[12..].Trim();
            else if (line.StartsWith("Warning=", StringComparison.OrdinalIgnoreCase))
                warning = line[8..].Trim();
        }

        // Flush the last block
        if (name is not null && packageName is not null)
            entries.Add(new AppxEntry(name, packageName, warning));

        return entries;
    }

    // --- Detection ---------------------------------------------------------------

    // Returns the subset of entries that are currently installed on this machine.
    public static async Task<List<AppxEntry>> ScanInstalledAsync(IEnumerable<AppxEntry> entries)
    {
        var installed = await GetInstalledNamesAsync();
        return FilterInstalled(entries, installed);
    }

    // Pure matching logic extracted for fast execution and unit testing.
    public static List<AppxEntry> FilterInstalled(IEnumerable<AppxEntry> entries, List<string> installed)
    {
        if (installed.Count == 0) return [];

        var installedSet = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);

        string? joined = null;
        ReadOnlySpan<char> joinedSpan = default;

        var result = new List<AppxEntry>();

        foreach (var e in entries)
        {
            string pkg = e.PackageName;
            if (string.IsNullOrEmpty(pkg))
            {
                result.Add(e);
                continue;
            }

            // O(1) fast path for exact package name matches
            if (installedSet.Contains(pkg))
            {
                result.Add(e);
                continue;
            }

            // Lazy creation of null-delimited single string span for fast SIMD substring search
            if (joined == null)
            {
                joined = string.Join('\0', installed);
                joinedSpan = joined.AsSpan();
            }

            if (joinedSpan.Contains(pkg.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                result.Add(e);
            }
        }

        return result;
    }

    // All package Names (not FullName) currently installed for the current user.
    public static async Task<List<string>> GetInstalledNamesAsync()
    {
        var output = await RunPsOutputAsync("(Get-AppxPackage).Name");
        return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    // --- Removal -----------------------------------------------------------------

    // Removes every package matching the entry's PackageName wildcard.
    // Returns true when PowerShell exits with code 0 (package gone or never existed).
    public static async Task<bool> RemoveAsync(AppxEntry entry)
    {
        if (!SecurityGuard.IsValidPackageName(entry.PackageName))
        {
            Debug.WriteLine($"[AppxService.RemoveAsync] Rejected invalid package name: {entry.PackageName}");
            return false;
        }

        var exitCode = await RunPsAsync(
            $"Get-AppxPackage -Name '*{entry.PackageName}*' | Remove-AppxPackage");
        return exitCode == 0;
    }

    // --- PowerShell helpers ------------------------------------------------------

    private static async Task<int> RunPsAsync(string command)
    {
        using var p = new Process { StartInfo = BuildPsi(command) };
        p.Start();
        await p.WaitForExitAsync();
        return p.ExitCode;
    }

    private static async Task<string> RunPsOutputAsync(string command)
    {
        var psi = BuildPsi(command);
        psi.RedirectStandardOutput = true;

        using var p = new Process { StartInfo = psi };
        p.Start();
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output;
    }

    private static ProcessStartInfo BuildPsi(string command) => new(SecurityGuard.GetSafePowerShellPath(),
        $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"")
    {
        UseShellExecute = false,
        CreateNoWindow  = true,
    };
}
