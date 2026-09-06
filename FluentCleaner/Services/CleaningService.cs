using FluentCleaner.Models;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Runtime.InteropServices;

namespace FluentCleaner.Services;

/* Two-phase clean cycle:
   Analyze ; walks FileKeys/RegKeys, builds a deletion list without touching anything
              Locked files (held open without FILE_SHARE_DELETE) are silently skipped, matching CCleaner behavior
   Clean   ; takes the completed ScanResult and does the actual deleting. */
public partial class CleaningService(PathExpander? expander = null)
{
    private readonly PathExpander _expander = expander ?? new();

    // --- Public API --------------------------------------------------
    public Task<ScanResult> AnalyzeAsync(CleanerEntry entry, IProgress<string>? progress = null, CancellationToken token = default)
        => Task.Run(() => Analyze(entry, progress, token), token);

    public Task<(int count, long bytes)> CleanAsync(ScanResult result, IProgress<string>? progress = null, CancellationToken token = default)
        => Task.Run(() => Clean(result, progress, token), token);

    // --- Analyze --------------------------------------------------

    /* Read-only phase. Walks FileKeys and RegKeys, builds the deletion list, touches nothing.
       Locked files get skipped here too; they'd fail at delete time anyway and would just
       inflate the reported size for no reason. */
    private ScanResult Analyze(CleanerEntry entry, IProgress<string>? progress, CancellationToken token = default)
    {
        var result   = new ScanResult { Entry = entry };
        var excluded = BuildExclusions(entry);
        var filesToDeleteSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Wrap the caller's progress so every path report is prefixed with the entry name.
        // e.g. "Firefox Cache >> C:\Users\...\Cache\Cache_Data"
        IProgress<string>? entryProgress = progress is null ? null
            : new PrefixedProgress(entry.Name, progress);

        foreach (var fileKey in entry.FileKeys)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var file in FindFiles(fileKey, excluded, entryProgress, token))
                {
                    if (filesToDeleteSet.Contains(file)) continue;

                    // Skip files that are truly inaccessible (hard lock / no permissions).
                    var size = TryGetDeletableSize(file);
                    if (size < 0) continue;

                    filesToDeleteSet.Add(file);
                    result.FilesToDelete.Add(file);
                    result.TotalBytes += size;
                }
            }
            catch (OperationCanceledException) { throw; }  // cancel must reach the caller
            catch (Exception ex) { Debug.WriteLine($"[CleaningService.Analyze] Error processing file key {fileKey.Path}: {ex.Message}"); }
        }

        foreach (var regKey in entry.RegKeys)
        {
            token.ThrowIfCancellationRequested();
            try { result.RegistryToDelete.AddRange(FindRegistryItems(regKey)); }
            catch (Exception ex) { Debug.WriteLine($"[CleaningService.Analyze] Error processing registry key {regKey.KeyPath}: {ex.Message}"); }
        }

        return result;
    }

    /* Resolves the FileKey path to real directories and yields every matching file.
       Patterns get split here upfront so the tree walk only happens once down below. */
    private IEnumerable<string> FindFiles(FileKeyEntry fileKey, List<ExclusionRule> excluded, IProgress<string>? progress, CancellationToken token = default)
    {
        bool recurse = fileKey.Flag is FileKeyFlag.Recurse or FileKeyFlag.RemoveSelf;

        var patterns = fileKey.Pattern
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dir in _expander.ResolvePaths(fileKey.Path))
        {
            token.ThrowIfCancellationRequested();
            if (!Directory.Exists(dir)) continue;
            if (!SecurityGuard.IsSafeDeletionPath(dir))
            {
                Debug.WriteLine($"[CleaningService.FindFiles] Skipping unsafe deletion path: {dir}");
                continue;
            }
            progress?.Report(dir);

            foreach (var f in EnumerateFilesSafe(dir, patterns, recurse, progress, token))
                if (!IsExcluded(f, excluded))
                    yield return f;
        }
    }

    /* Walks the tree once; lets the OS match files per pattern (FindFirstFile knows about
       8.3 short-name aliases, we don't). HashSet drops files that match more than one pattern.
       Reparse points skipped to prevent infinite junction loop traps. */
    private static IEnumerable<string> EnumerateFilesSafe(string root, string[] patterns, bool recurse, IProgress<string>? progress = null, CancellationToken token = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in patterns)
        {
            token.ThrowIfCancellationRequested();
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, p); }
            catch (Exception ex) { Debug.WriteLine($"[CleaningService.EnumerateFilesSafe] Error enumerating files in {root} with pattern {p}: {ex.Message}"); files = []; }
            foreach (var f in files)
                if (seen.Add(f))   // skip if another pattern already matched this file
                    yield return f;
        }

        if (!recurse) yield break;

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root)
                            .Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) == 0);
        }
        catch (Exception ex) { Debug.WriteLine($"[CleaningService.EnumerateFilesSafe] Error enumerating directories in {root}: {ex.Message}"); yield break; }

        foreach (var sub in dirs)
        {
            token.ThrowIfCancellationRequested(); // check per sub-folder
            progress?.Report(sub);
            foreach (var f in EnumerateFilesSafe(sub, patterns, recurse: true, progress, token))
                yield return f;
        }
    }

    // Checks whether a registry key/value exists before queuing it for deletion
    private static IEnumerable<RegistryItemToDelete> FindRegistryItems(RegKeyEntry regKey)
    {
        var (hive, subKey) = RegistryHelper.SplitHiveSubKey(regKey.KeyPath);
        if (!SecurityGuard.IsSafeRegistryDeletion(hive, subKey, regKey.ValueName))
        {
            Debug.WriteLine($"[CleaningService.FindRegistryItems] Skipping unsafe registry target: {regKey.KeyPath}");
            yield break;
        }

        using var root = RegistryHelper.OpenHive(hive);
        if (root is null) yield break;

        using var key = root.OpenSubKey(subKey, writable: false);
        if (key is null) yield break;

        if (regKey.ValueName is not null)
        {
            if (key.GetValue(regKey.ValueName) is not null)
                yield return new RegistryItemToDelete { KeyPath = regKey.KeyPath, ValueName = regKey.ValueName };
        }
        else
        {
            yield return new RegistryItemToDelete { KeyPath = regKey.KeyPath };
        }
    }

    // --- Clean ----------------------------------------------------

    /* Deletes everything the Analyze phase queued up.
       Files that are in use or already gone get skipped silently.
       Returns the count of successfully deleted items and the total bytes freed. */
    private (int count, long bytes) Clean(ScanResult result, IProgress<string>? progress, CancellationToken token = default)
    {
        int  count = 0;
        long bytes = 0;

        foreach (var file in result.FilesToDelete)
        {
            token.ThrowIfCancellationRequested(); // stop between files so we never delete half an entry
            try
            {
                var fi = new FileInfo(file);
                if (!fi.Exists) continue;
                var size = fi.Length;
                if (fi.IsReadOnly)
                    fi.IsReadOnly = false;
                fi.Delete();
                count++;
                bytes += size;
                progress?.Report(ResourceService.Fmt("Prog_Deleted", file));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Debug.WriteLine($"[CleaningService.Clean] Failed to delete file {file}: {ex.Message}"); }
        }

        foreach (var regItem in result.RegistryToDelete)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                DeleteRegistryItem(regItem);
                count++;
                progress?.Report(ResourceService.Fmt("Prog_Registry", regItem));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Debug.WriteLine($"[CleaningService.Clean] Failed to delete registry item {regItem.KeyPath}: {ex.Message}"); }
        }

        // REMOVESELF: prune directories that are now empty
        foreach (var fk in result.Entry.FileKeys.Where(fk => fk.Flag == FileKeyFlag.RemoveSelf))
            foreach (var resolved in _expander.ResolvePaths(fk.Path))
                TryPruneEmptyDirs(resolved);

        return (count, bytes);
    }

    /* Deletes a single registry value or an entire key tree, depending on whether
       ValueName is set. Both paths are no-ops if the target no longer exists. */
    private static void DeleteRegistryItem(RegistryItemToDelete item)
    {
        var (hive, subKey) = RegistryHelper.SplitHiveSubKey(item.KeyPath);
        if (!SecurityGuard.IsSafeRegistryDeletion(hive, subKey, item.ValueName))
        {
            Debug.WriteLine($"[CleaningService.DeleteRegistryItem] Skipping unsafe registry deletion: {item.KeyPath}");
            return;
        }

        using var root = RegistryHelper.OpenHive(hive);
        if (root is null) return;

        if (item.ValueName is not null)
        {
            using var key = root.OpenSubKey(subKey, writable: true);
            key?.DeleteValue(item.ValueName, throwOnMissingValue: false);
        }
        else
        {
            var parentSubKey = Path.GetDirectoryName(subKey)?.Replace('/', '\\') ?? "";
            var keyName      = Path.GetFileName(subKey);
            using var parent = root.OpenSubKey(parentSubKey, writable: true);
            parent?.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
        }
    }

    /* Cleans up empty folders left behind by a REMOVESELF clean.
       Order matters: deepest first, so parent directories become empty before we try to delete them. */
    private static void TryPruneEmptyDirs(string path)
    {
        if (!Directory.Exists(path) || !SecurityGuard.IsSafeDeletionPath(path)) return;
        try
        {
            foreach (var sub in Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                                         .OrderByDescending(d => d.Length))
            {
                if (!SecurityGuard.IsSafeDeletionPath(sub)) continue;
                if (Directory.GetFileSystemEntries(sub).Length == 0)
                    Directory.Delete(sub);
            }

            if (SecurityGuard.IsSafeDeletionPath(path) && Directory.GetFileSystemEntries(path).Length == 0)
                Directory.Delete(path);
        }
        catch (Exception ex) { Debug.WriteLine($"[CleaningService.TryPruneEmptyDirs] Failed to prune empty directories in {path}: {ex.Message}"); }
    }

    // --- Helpers --------------------------------------------------

    private List<ExclusionRule> BuildExclusions(CleanerEntry entry)
    {
        var rules = new List<ExclusionRule>();

        foreach (var ex in entry.ExcludeKeys)
            AddRule(ex, rules);

        var settings = AppSettings.Instance;
        if (settings.GlobalExclusionsEnabled)
            foreach (var line in settings.GlobalExclusions)
                AddRule(ExcludeKeyEntry.Parse(line), rules);

        return rules;
    }

    private void AddRule(ExcludeKeyEntry ex, List<ExclusionRule> rules)
    {
        if (ex.Type is ExcludeType.Reg) return;
        foreach (var p in _expander.ResolvePaths(ex.Path))
            rules.Add(new ExclusionRule(p.TrimEnd('\\') + "\\", ex.Pattern));
    }

    // Probe whether a file is deletable right now by requesting DELETE access via CreateFileW.
    private static long TryGetDeletableSize(string path)
    {
        const uint DELETE = 0x00010000;
        const uint FILE_SHARE_ALL = 0x7;   // Read | Write | Delete
        const uint OPEN_EXISTING = 3;

        using var handle = CreateFileW(path, DELETE, FILE_SHARE_ALL,
                                       IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid) return -1;   // locked; skip!

        try { return new FileInfo(path).Length; }
        catch (Exception ex) { Debug.WriteLine($"[CleaningService.TryGetDeletableSize] Failed to get length of {path}: {ex.Message}"); return -1; }
    }

    private static bool IsExcluded(string path, List<ExclusionRule> rules)
    {
        foreach (var rule in rules)
            if (rule.Matches(path))
                return true;
        return false;
    }

    // --- Nested Types ---------------------------------------------

    private readonly record struct ExclusionRule(string DirPrefix, string? Pattern)
    {
        public bool Matches(string filePath)
        {
            if (!filePath.StartsWith(DirPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            if (Pattern is null) return true;

            if (Pattern.Contains('*') || Pattern.Contains('?'))
            {
                var fileName = Path.GetFileName(filePath);
                return FileSystemName.MatchesSimpleExpression(Pattern, fileName, ignoreCase: true);
            }

            var relativePath = filePath[DirPrefix.Length..];
            return relativePath.Equals(Pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class PrefixedProgress(string prefix, IProgress<string> inner) : IProgress<string>
    {
        public void Report(string path) => inner.Report($"{prefix}  ›  {path}");
    }

    // --- P/Invoke -------------------------------------------------

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);
}