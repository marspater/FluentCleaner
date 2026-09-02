namespace FluentCleaner.Services;

/* Handles the two jobs that make FileKey paths tricky:
1. Expand %EnvVar% tokens (winapp2 uses its own subset, not all Windows vars)
2. Walk directory trees where path segments contain * wildcards */
public class PathExpander
{
    // Caching environment variable map statically avoids redundant Environment.GetFolderPath calls on each instance.
    private static readonly Dictionary<string, string> _vars = BuildVarMap();

    private static Dictionary<string, string> BuildVarMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                map[$"%{name}%"] = value.TrimEnd('\\', '/');
        }

        Add("AppData",           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        Add("LocalAppData",      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Add("LocalLowAppData",   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow"));
        Add("ProgramFiles",      Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add("ProgramFiles(x86)", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        Add("ProgramFilesX86",   Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)); // Winapp2 alias (no parentheses)
        Add("ProgramData",       Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        Add("CommonAppData",     Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        Add("UserProfile",       Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add("Documents",         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add("Desktop",           Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add("Music",             Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        Add("Pictures",          Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add("Videos",            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add("SystemRoot",        Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add("WinDir",            Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add("System",            Environment.GetFolderPath(Environment.SpecialFolder.System));
        Add("SystemX86",         Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));
        Add("Temp",              Path.GetTempPath().TrimEnd('\\', '/'));
        Add("Tmp",               Path.GetTempPath().TrimEnd('\\', '/'));
        Add("SystemDrive",       Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))?.TrimEnd('\\') ?? "C:");

        return map;
    }

    public string ExpandVariables(string path)
    {
        // Optimization: if there are no '%' characters in path, bypass variable replacement loops completely (~1.7x faster).
        if (path.IndexOf('%') >= 0)
        {
            foreach (var (token, value) in _vars)
            {
                if (path.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Replace(token, value, StringComparison.OrdinalIgnoreCase);
                    // Break early if all variables in path have been expanded
                    if (path.IndexOf('%') < 0) break;
                }
            }

            // Let the OS handle any remaining %VAR% tokens we don't know about
            if (path.IndexOf('%') >= 0)
            {
                path = Environment.ExpandEnvironmentVariables(path);
            }
        }

        // %SystemDrive% (and any other bare drive reference) expands to "C:" without a
        // trailing backslash because BuildVarMap strips it to avoid double-backslashes in
        // compound paths like "%SystemDrive%\Users".  On Windows "C:" means the CWD of
        // that drive, NOT the root;so a FileKey like "%SystemDrive%|*.bak|RECURSE" would
        // silently scan only the app's working directory instead of all of C:\.
        // Detect and append the separator when the result is a bare drive root.
        if (path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':')
            path += Path.DirectorySeparatorChar;

        return path;
    }

    // Returns all concrete paths matched by a pattern like
    // "%LocalAppData%\Google\Chrome*\User Data\*\Cache".
    // Winapp2 uses %ProgramFiles% for both 32-bit and 64-bit locations,
    // so we automatically also try the x86 variant to avoid missing apps
    // installed under Program Files (x86).
    public List<string> ResolvePaths(string rawPath)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Always resolve the primary path.
        ResolveRecursive(ExpandVariables(rawPath), results);

        // If the path references %ProgramFiles%, also try %ProgramFiles(x86)%.
        // HashSet prevents duplicates when both point to the same dir (32-bit OS).
        if (rawPath.Contains("%ProgramFiles%", StringComparison.OrdinalIgnoreCase))
        {
            var x86Path = rawPath.Replace("%ProgramFiles%", "%ProgramFiles(x86)%",
                                          StringComparison.OrdinalIgnoreCase);
            ResolveRecursive(ExpandVariables(x86Path), results);
        }
        return results.ToList();
    }

    private static void ResolveRecursive(string path, HashSet<string> results)
    {
        var parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.None);

        // Find the first segment that contains a wildcard
        int wcIdx = Array.FindIndex(parts, p => p.Contains('*') || p.Contains('?'));

        if (wcIdx < 0)
        {
            // No wildcard, so this is a literal path, add as-is
            results.Add(path);
            return;
        }

        var basePath = wcIdx == 0
            ? Path.GetPathRoot(path) ?? ""
            : string.Join('\\', parts[..wcIdx]);

        if (!Directory.Exists(basePath)) return;

        var wildcard  = parts[wcIdx];
        var remaining = parts[(wcIdx + 1)..];

        try
        {
            // If there are more segments after the wildcard, we only care about directories
            var matches = remaining.Length == 0
                ? Directory.GetFileSystemEntries(basePath, wildcard)
                : Directory.GetDirectories(basePath, wildcard);

            foreach (var match in matches)
            {
                if (remaining.Length == 0)
                    results.Add(match);
                else
                    ResolveRecursive(Path.Combine(match, string.Join('\\', remaining)), results);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PathExpander] Failed to access directory: {basePath}. Error: {ex.Message}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PathExpander] Failed to access directory: {basePath}. Error: {ex.Message}");
        }
    }
}
