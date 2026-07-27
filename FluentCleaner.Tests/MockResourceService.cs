using System.Globalization;

namespace FluentCleaner.Services;

public static class ResourceService
{
    public static string Get(string key) => key;
    public static string Fmt(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, key, args);
}
