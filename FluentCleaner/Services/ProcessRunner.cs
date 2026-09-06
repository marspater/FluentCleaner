namespace FluentCleaner.Services;

// Helper to safely execute user-configured post-clean commands without shell execution (cmd.exe /c)
public static class ProcessRunner
{
    private static readonly string[] ExecutableExtensions = [".exe", ".bat", ".cmd", ".ps1", ".com", ".msi", ".scr", ".vbs"];

    // Parses a raw command-line string into a binary path (FileName) and its arguments.
    // Handles double-quoted executables as well as space-delimited command strings.
    // Checks disk existence and executable extensions to resolve unquoted paths with spaces safely.
    public static (string FileName, string Arguments) ParseCommandLine(string commandLine, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;

        if (string.IsNullOrWhiteSpace(commandLine))
            return (string.Empty, string.Empty);

        string trimmed = commandLine.Trim();
        if (trimmed.StartsWith('"'))
        {
            int closingQuoteIndex = trimmed.IndexOf('"', 1);
            if (closingQuoteIndex > 0)
            {
                string fileName = trimmed[1..closingQuoteIndex].Trim();
                string arguments = trimmed[(closingQuoteIndex + 1)..].Trim();
                return (fileName, arguments);
            }
        }

        var candidates = GetCandidates(trimmed);

        // Pass 1: Check if any candidate fileName exists on disk (longest candidate first)
        foreach (var (candFileName, candArgs) in candidates)
        {
            if (fileExists(candFileName))
            {
                return (candFileName, candArgs);
            }
        }

        // Pass 2: Check for known executable extension (e.g. .exe, .bat, .cmd, .ps1)
        foreach (var (candFileName, candArgs) in candidates)
        {
            foreach (var ext in ExecutableExtensions)
            {
                if (candFileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return (candFileName, candArgs);
                }
            }
        }

        // Pass 3: Check if argument starts with a standard switch prefix (/, -, --) or quote
        foreach (var (candFileName, candArgs) in candidates)
        {
            if (!string.IsNullOrEmpty(candArgs))
            {
                if (candArgs.StartsWith('/') || candArgs.StartsWith('-') || candArgs.StartsWith('"') || candArgs.StartsWith("'"[0]))
                {
                    return (candFileName, candArgs);
                }
            }
        }

        // Fallback: Split at first space/tab if any, or return trimmed as binary path
        int firstSpaceIndex = trimmed.IndexOfAny([' ', '\t']);
        if (firstSpaceIndex >= 0)
        {
            string fileName = trimmed[..firstSpaceIndex].Trim();
            string arguments = trimmed[(firstSpaceIndex + 1)..].Trim();
            return (fileName, arguments);
        }

        return (trimmed, string.Empty);
    }

    private static List<(string FileName, string Arguments)> GetCandidates(string trimmed)
    {
        var candidates = new List<(string FileName, string Arguments)>
        {
            (trimmed, string.Empty)
        };

        for (int i = trimmed.Length - 1; i >= 0; i--)
        {
            char c = trimmed[i];
            if (c == ' ' || c == '\t')
            {
                string fileName = trimmed[..i].Trim();
                string arguments = trimmed[(i + 1)..].Trim();
                if (!string.IsNullOrEmpty(fileName))
                {
                    candidates.Add((fileName, arguments));
                }
            }
        }

        return candidates;
    }

    // Runs a command directly with UseShellExecute = false, bypassing cmd.exe /c to prevent command injection.
    public static async Task RunCommandAsync(string line)
    {
        var (fileName, arguments) = ParseCommandLine(line);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName         = fileName,
            Arguments        = arguments,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute  = false,
            CreateNoWindow   = true
        });

        if (process is not null)
        {
            await process.WaitForExitAsync();
        }
    }
}
