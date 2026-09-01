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

    // High-performance span-based parser to avoid string splitting allocations
    public static ExcludeKeyEntry Parse(ReadOnlySpan<char> value)
    {
        var p1 = value.IndexOf('|');
        if (p1 < 0)
        {
            var typeStr = value.Trim();
            var type = typeStr.Equals("REG", StringComparison.OrdinalIgnoreCase) ? ExcludeType.Reg :
                       typeStr.Equals("PATH", StringComparison.OrdinalIgnoreCase) ? ExcludeType.Path : ExcludeType.File;
            return new ExcludeKeyEntry { Type = type };
        }

        var typeSpan = value[..p1].Trim();
        var exType = typeSpan.Equals("REG", StringComparison.OrdinalIgnoreCase) ? ExcludeType.Reg :
                     typeSpan.Equals("PATH", StringComparison.OrdinalIgnoreCase) ? ExcludeType.Path : ExcludeType.File;

        var rem = value[(p1 + 1)..];
        var p2 = rem.IndexOf('|');

        if (p2 < 0)
        {
            return new ExcludeKeyEntry { Type = exType, Path = rem.Trim().ToString() };
        }

        var pathSpan = rem[..p2].Trim();
        var patSpan = rem[(p2 + 1)..].Trim();

        return new ExcludeKeyEntry
        {
            Type = exType,
            Path = pathSpan.ToString(),
            Pattern = patSpan.IsEmpty ? null : patSpan.ToString()
        };
    }
}
