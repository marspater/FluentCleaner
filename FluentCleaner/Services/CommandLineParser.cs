namespace FluentCleaner.Services;

public static class CommandLineParser
{
    public static (string fileName, string arguments) Parse(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return (string.Empty, string.Empty);

        string trimmed = commandLine.Trim();

        if (trimmed.StartsWith('"'))
        {
            int closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote != -1)
            {
                string fileName = trimmed[1..closingQuote];
                string arguments = trimmed[(closingQuote + 1)..].TrimStart();
                return (fileName, arguments);
            }
        }

        int spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex != -1)
        {
            string fileName = trimmed[..spaceIndex];
            string arguments = trimmed[(spaceIndex + 1)..].TrimStart();
            return (fileName, arguments);
        }

        return (trimmed, string.Empty);
    }
}
