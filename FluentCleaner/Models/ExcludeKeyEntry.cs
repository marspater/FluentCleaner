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

    // Performance optimization: Span-based parsing avoids string.Split('|') array allocations
    // and intermediate substring allocations when parsing ExcludeKey lines.
    public static ExcludeKeyEntry Parse(ReadOnlySpan<char> value)
    {
        var entry = new ExcludeKeyEntry();
        var firstPipe = value.IndexOf('|');

        if (firstPipe < 0)
        {
            entry.Type = ParseType(value.Trim());
            return entry;
        }

        entry.Type = ParseType(value[..firstPipe].Trim());

        var remainder = value[(firstPipe + 1)..];
        var secondPipe = remainder.IndexOf('|');

        if (secondPipe < 0)
        {
            entry.Path = remainder.Trim().ToString();
        }
        else
        {
            entry.Path = remainder[..secondPipe].Trim().ToString();
            var pattern = remainder[(secondPipe + 1)..].Trim();
            if (!pattern.IsEmpty)
                entry.Pattern = pattern.ToString();
        }

        return entry;
    }

    private static ExcludeType ParseType(ReadOnlySpan<char> typeSpan)
    {
        if (typeSpan.Equals("FILE", StringComparison.OrdinalIgnoreCase)) return ExcludeType.File;
        if (typeSpan.Equals("PATH", StringComparison.OrdinalIgnoreCase)) return ExcludeType.Path;
        if (typeSpan.Equals("REG", StringComparison.OrdinalIgnoreCase)) return ExcludeType.Reg;
        return ExcludeType.File;
    }
}
