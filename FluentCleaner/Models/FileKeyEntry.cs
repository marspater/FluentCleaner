namespace FluentCleaner.Models;

// Controls how a FileKey directory is scanned.
public enum FileKeyFlag
{
    None,       // Top-level files only.
    Recurse,    // Scan all subdirectories recursively.
    RemoveSelf  // Like Recurse, but also prune empty directories afterwards.
}

/* Parsed representation of one FileKeyN= line from Winapp2.ini.
   Format:  FileKey1=<path>|<pattern(s)>[|RECURSE|REMOVESELF]
   Example: FileKey1=%LocalAppData%\Temp|*.tmp;*.log|RECURSE */
public class FileKeyEntry
{
    // Directory path to scan. May contain %EnvVar% tokens and * wildcards in path segments.
    public string Path { get; set; } = "";

    /* Semicolon-separated file filter(s), e.g. "*.tmp" or "*.log;*.bak".
       Defaults to "*.*" when no pattern is specified in the ini. */
    public string Pattern { get; set; } = "*.*";

    // Whether to recurse into subdirectories and whether to remove empty dirs afterwards.
    public FileKeyFlag Flag { get; set; } = FileKeyFlag.None;

    public static FileKeyEntry Parse(string value) => Parse(value.AsSpan());

    // High-performance span-based parser to avoid string splitting allocations
    public static FileKeyEntry Parse(ReadOnlySpan<char> value)
    {
        var p1 = value.IndexOf('|');
        if (p1 < 0) return new FileKeyEntry { Path = value.Trim().ToString() };

        var path = value[..p1].Trim().ToString();
        var rem = value[(p1 + 1)..];
        var p2 = rem.IndexOf('|');

        string pattern = "*.*";
        FileKeyFlag flag = FileKeyFlag.None;

        if (p2 < 0)
        {
            var p = rem.Trim();
            if (p.Equals("RECURSE", StringComparison.OrdinalIgnoreCase))
                flag = FileKeyFlag.Recurse;
            else if (p.Equals("REMOVESELF", StringComparison.OrdinalIgnoreCase))
                flag = FileKeyFlag.RemoveSelf;
            else if (!p.IsEmpty)
                pattern = p.ToString();
        }
        else
        {
            var pat = rem[..p2].Trim();
            if (!pat.IsEmpty)
                pattern = pat.ToString();

            var rem2 = rem[(p2 + 1)..];
            var p3 = rem2.IndexOf('|');
            var flagSpan = (p3 < 0 ? rem2 : rem2[..p3]).Trim();

            if (flagSpan.Equals("RECURSE", StringComparison.OrdinalIgnoreCase))
                flag = FileKeyFlag.Recurse;
            else if (flagSpan.Equals("REMOVESELF", StringComparison.OrdinalIgnoreCase))
                flag = FileKeyFlag.RemoveSelf;
        }

        return new FileKeyEntry { Path = path, Pattern = pattern, Flag = flag };
    }
}
