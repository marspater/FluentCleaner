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
    public string Path
    {
        get => field;
        set => field = value?.Trim() ?? "";
    } = "";

    /* Semicolon-separated file filter(s), e.g. "*.tmp" or "*.log;*.bak".
       Defaults to "*.*" when no pattern is specified in the ini. */
    public string Pattern
    {
        get => field;
        set => field = string.IsNullOrWhiteSpace(value) ? "*.*" : value.Trim();
    } = "*.*";

    // Whether to recurse into subdirectories and whether to remove empty dirs afterwards.
    public FileKeyFlag Flag { get; set; } = FileKeyFlag.None;

    public static FileKeyEntry Parse(string value) => Parse(value.AsSpan());

    public static FileKeyEntry Parse(ReadOnlySpan<char> value)
    {
        var firstPipe = value.IndexOf('|');
        if (firstPipe < 0)
        {
            return new FileKeyEntry { Path = value.Trim().ToString() };
        }

        var path = value[..firstPipe].Trim().ToString();
        var remainder = value[(firstPipe + 1)..];
        var secondPipe = remainder.IndexOf('|');

        var entry = new FileKeyEntry { Path = path };

        if (secondPipe < 0)
        {
            var p = remainder.Trim();
            if (p.Equals("RECURSE", StringComparison.OrdinalIgnoreCase))
                entry.Flag = FileKeyFlag.Recurse;
            else if (p.Equals("REMOVESELF", StringComparison.OrdinalIgnoreCase))
                entry.Flag = FileKeyFlag.RemoveSelf;
            else if (!p.IsEmpty)
                entry.Pattern = p.ToString();
        }
        else
        {
            var pattern = remainder[..secondPipe].Trim();
            if (!pattern.IsEmpty)
                entry.Pattern = pattern.ToString();

            var flagSpan = remainder[(secondPipe + 1)..].Trim();
            if (flagSpan.Equals("RECURSE", StringComparison.OrdinalIgnoreCase))
                entry.Flag = FileKeyFlag.Recurse;
            else if (flagSpan.Equals("REMOVESELF", StringComparison.OrdinalIgnoreCase))
                entry.Flag = FileKeyFlag.RemoveSelf;
        }

        return entry;
    }
}
