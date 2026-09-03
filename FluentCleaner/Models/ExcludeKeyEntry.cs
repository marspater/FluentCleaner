namespace FluentCleaner.Models;

//What kind of resource the ExcludeKey protects.
public enum ExcludeType
{
    File,  // Specific file or filename pattern inside a directory.
    Path,  // Entire directory subtree.
    Reg    // Registry key or value (not processed during file scanning).
}

/* Parsed representation of one ExcludeKeyN= line from Winapp2.ini.
   Format:  ExcludeKey1=<TYPE>|<path>[|<filename pattern>]
   Example: ExcludeKey1=FILE|%AppData%\Mozilla\Firefox\Profiles\|places.sqlite */
public class ExcludeKeyEntry
{
    // Whether this exclusion covers a file, a directory tree, or a registry entry.
    public ExcludeType Type { get; set; }

    // Base path of the exclusion. May contain %EnvVar% tokens.
    public string Path { get; set; } = "";

    /* Optional filename or pattern within Path (e.g. "places.sqlite" or "*.db").
       When set, only that specific file is excluded and not the whole directory.
       When null, the entire directory is excluded (PATH-style behaviour). */
    public string? Pattern { get; set; }

    public static ExcludeKeyEntry Parse(string value) => Parse(value.AsSpan());

    public static ExcludeKeyEntry Parse(ReadOnlySpan<char> value)
    {
        var entry = new ExcludeKeyEntry();

        int firstPipe = value.IndexOf('|');
        if (firstPipe < 0)
        {
            var typeSpan = value.Trim();
            entry.Type = ParseType(typeSpan);
            return entry;
        }

        var typeStr = value[..firstPipe].Trim();
        entry.Type = ParseType(typeStr);

        var rest = value[(firstPipe + 1)..];
        int secondPipe = rest.IndexOf('|');
        if (secondPipe < 0)
        {
            entry.Path = rest.Trim().ToString();
        }
        else
        {
            entry.Path = rest[..secondPipe].Trim().ToString();
            var patternSpan = rest[(secondPipe + 1)..].Trim();
            if (!patternSpan.IsEmpty)
                entry.Pattern = patternSpan.ToString();
        }

        return entry;
    }

    private static ExcludeType ParseType(ReadOnlySpan<char> span)
    {
        if (span.Equals("FILE", StringComparison.OrdinalIgnoreCase)) return ExcludeType.File;
        if (span.Equals("PATH", StringComparison.OrdinalIgnoreCase)) return ExcludeType.Path;
        if (span.Equals("REG", StringComparison.OrdinalIgnoreCase)) return ExcludeType.Reg;
        return ExcludeType.File;
    }
}
