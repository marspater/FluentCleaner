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

    public static FileKeyEntry Parse(ReadOnlySpan<char> value)
    {
        int firstPipe = value.IndexOf('|');
        if (firstPipe < 0)
        {
            return new FileKeyEntry { Path = value.Trim().ToString() };
        }

        var pathSpan = value[..firstPipe].Trim();
        var rest = value[(firstPipe + 1)..];

        int secondPipe = rest.IndexOf('|');
        if (secondPipe < 0)
        {
            var p = rest.Trim();
            if (p.Equals("RECURSE", StringComparison.OrdinalIgnoreCase))
                return new FileKeyEntry { Path = pathSpan.ToString(), Flag = FileKeyFlag.Recurse };
            if (p.Equals("REMOVESELF", StringComparison.OrdinalIgnoreCase))
                return new FileKeyEntry { Path = pathSpan.ToString(), Flag = FileKeyFlag.RemoveSelf };

            var pattern = p.IsEmpty ? "*.*" : p.ToString();
            return new FileKeyEntry { Path = pathSpan.ToString(), Pattern = pattern };
        }
        else
        {
            var patternSpan = rest[..secondPipe].Trim();
            var flagSpan = rest[(secondPipe + 1)..].Trim();

            int thirdPipe = flagSpan.IndexOf('|');
            if (thirdPipe >= 0) flagSpan = flagSpan[..thirdPipe].Trim();

            var pattern = patternSpan.IsEmpty ? "*.*" : patternSpan.ToString();
            FileKeyFlag flag = FileKeyFlag.None;
            if (flagSpan.Equals("RECURSE", StringComparison.OrdinalIgnoreCase))
                flag = FileKeyFlag.Recurse;
            else if (flagSpan.Equals("REMOVESELF", StringComparison.OrdinalIgnoreCase))
                flag = FileKeyFlag.RemoveSelf;

            return new FileKeyEntry { Path = pathSpan.ToString(), Pattern = pattern, Flag = flag };
        }
    }
}
