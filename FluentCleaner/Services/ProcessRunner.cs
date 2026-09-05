namespace FluentCleaner.Services;

// Helper to safely execute user-configured post-clean commands without shell execution (cmd.exe /c)
public static class ProcessRunner
{
    // Parses a raw command-line string into a binary path (FileName) and its arguments.
    // Handles double-quoted executables as well as space-delimited command strings.
    public static (string FileName, string Arguments) ParseCommandLine(string commandLine)
    {
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

        int spaceIndex = trimmed.IndexOfAny([' ', '\t']);
        if (spaceIndex >= 0)
        {
            string fileName = trimmed[..spaceIndex].Trim();
            string arguments = trimmed[(spaceIndex + 1)..].Trim();
            return (fileName, arguments);
        }

        return (trimmed, string.Empty);
    }

    // Runs a command directly with UseShellExecute = false, bypassing cmd.exe /c to prevent command injection.
    public static async Task RunCommandAsync(string line)
    {
        var (fileName, arguments) = ParseCommandLine(line);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = fileName,
            Arguments       = arguments,
            UseShellExecute = false,
            CreateNoWindow  = true
        });

        if (process is not null)
        {
            await process.WaitForExitAsync();
        }
    }
}
