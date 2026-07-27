using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentCleaner.Models;

namespace FluentCleaner.Services;

/* Talks to Groq (llama-3.3-70b) to explain a Winapp2 entry in plain English.
   The real file paths and registry keys go into the prompt so the answer is actually about
   what THIS entry does, not some generic Winapp2 boilerplate.
   Groq has been free for me so far; grab a key at console.groq.com.
   Cached in-memory so reopening the same entry is instant. */
public static class AiExplainer
{
    private static readonly HttpClient _http = new();
    private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<string> ExplainAsync(CleanerEntry entry)
    {
        if (_cache.TryGetValue(entry.Name, out var cached))
            return cached;

        var apiKey = AppSettings.Instance.GroqApiKey
                     ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return ResourceService.Get("AI_NoKey");

        var prompt = BuildPrompt(entry);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    model      = "llama-3.3-70b-versatile",
                    max_tokens = 300,
                    messages   = new[]
                    {
                        new { role = "system", content = "You are a Windows PC expert. Explain Winapp2 cleaner entries concisely and accurately based on the file paths and registry keys provided." + LangInstruction() },
                        new { role = "user",   content = prompt }
                    }
                }),
                Encoding.UTF8, "application/json");

            var res  = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                return ResourceService.Fmt("AI_ApiError", msg ?? string.Empty);
            }

            var text = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? ResourceService.Get("AI_NoResponse");

            _cache[entry.Name] = text;
            return text;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return ResourceService.Fmt("AI_NetworkError", ex.Message);
        }
    }

    // Generates a Winapp2 INI entry from a plain-English description.
    public static Task<string> GenerateEntryAsync(string description) =>
        GenerateAsync(
            userMsg: $"Generate a Winapp2 cleaner entry for: {description}",
            errorPrefix: "; ",
            systemPrompt:
                "You are a Winapp2 database expert. Generate a valid Winapp2 INI cleaner entry from the user description. " +
                "Output ONLY raw INI — no explanation, no markdown, no code fences.\n" +
                "STRUCTURE: [App Name]\n" +
                "DETECTION (include at least one):\n" +
                "  DetectKey=HKLM\\Software\\App  or  HKLM\\Software\\App|ValueName\n" +
                "  DetectFile=%LocalAppData%\\App\\*\n" +
                "  SpecialDetect=DET_CHROME|DET_FIREFOX|DET_EDGE|DET_OPERA|DET_THUNDERBIRD|DET_IE|DET_WINSTORE\n" +
                "  Multiple detect lines use OR logic.\n" +
                "FILE KEYS:\n" +
                "  FileKey1=PATH|PATTERN  or  PATH|PATTERN|RECURSE  or  PATH|PATTERN|REMOVESELF\n" +
                "  Multiple patterns: FileKey1=PATH|*.log;*.tmp;*.bak\n" +
                "REGISTRY KEYS:\n" +
                "  RegKey1=HKCU\\Software\\App  or  HKCU\\Software\\App|ValueName\n" +
                "  Hives: HKCU HKLM HKCR HKU HKCC\n" +
                "EXCLUDE KEYS:\n" +
                "  ExcludeKey1=FILE|%AppData%\\App\\|important.dat\n" +
                "  ExcludeKey2=PATH|%AppData%\\App\\Keep\\\n" +
                "PATH VARIABLES:\n" +
                "  %AppData% %LocalAppData% %LocalLowAppData% %ProgramData%\n" +
                "  %ProgramFiles% %ProgramFiles(x86)% %UserProfile%\n" +
                "  %SystemRoot% %System% %SystemDrive% %Temp%\n" +
                "  %Documents% %Desktop% %Music% %Pictures% %Videos%\n" +
                "OTHER FIELDS: Section=  Warning=  Default=True|False");

    // Generates a PowerShell cleanup script from a plain-English description.
    public static Task<string> GenerateScriptAsync(string description) =>
        GenerateAsync(
            userMsg: $"Generate a PowerShell script for: {description}",
            errorPrefix: "# ",
            systemPrompt:
                "You are a Windows PowerShell expert. Generate a practical, well-written PowerShell script based on the user description. " +
                "Output ONLY raw PowerShell — no explanation, no markdown, no code fences.\n" +
                "RULES:\n" +
                "  - First line MUST be a # comment briefly describing what the script does.\n" +
                "  - Use $env: variables where appropriate: $env:LOCALAPPDATA $env:APPDATA $env:TEMP $env:USERPROFILE $env:ProgramFiles $env:SystemRoot.\n" +
                "  - Always use -ErrorAction SilentlyContinue or try/catch — never let the script crash.\n" +
                "  - Use Write-Host to report progress.\n" +
                "  - NEVER use Invoke-Expression or download and execute remote code.");

    // Shared HTTP helper used by GenerateEntryAsync and GenerateScriptAsync.
    private static async Task<string> GenerateAsync(string userMsg, string systemPrompt, string errorPrefix)
    {
        var apiKey = AppSettings.Instance.GroqApiKey
                     ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return $"{errorPrefix}{ResourceService.Get("AI_NoKeyShort")}";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    model      = "llama-3.3-70b-versatile",
                    max_tokens = 500,
                    messages   = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = userMsg }
                    }
                }),
                Encoding.UTF8, "application/json");

            var res  = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                return $"{errorPrefix}Groq error: {msg}";
            }

            return root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? $"{errorPrefix}No response received.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return $"{errorPrefix}Could not reach Groq API: {ex.Message}";
        }
    }

    //just a quick key test;asks Groq one sentence about FluentCleaner; returns "✓ " or "✗"
    public static async Task<string> TestKeyAsync(string apiKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    model      = "llama-3.3-70b-versatile",
                    max_tokens = 150,
                    messages   = new[]
                    {
                        new { role = "user", content = "Describe FluentCleaner in 2 short sentences. " +
                        "Facts: open-source, built solo and fueled by coffee, written in C# on .NET 10 + WinUI 3, " +
                        "native Windows 11 UI, no telemetry, uses the Winapp2 database. " +
                        "Do NOT mention AI, machine learning or any AI-related features. Keep it factual." }
                    }
                }),
                Encoding.UTF8, "application/json");

            var res  = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
                return "✗ " + (err.TryGetProperty("message", out var m) ? m.GetString() : "API error");

            var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return "✓ " + text;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return "✗ " + ex.Message;
        }
    }

    // Returns " Please respond in German." etc. so empty only for English.
    // Falls back to the Windows UI culture when we pick System default
    private static string LangInstruction()
    {
        var lang = AppSettings.Instance.Language;
        if (string.IsNullOrWhiteSpace(lang))
            lang = CultureInfo.CurrentUICulture.Name;

        if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        try
        {
            var name = CultureInfo.GetCultureInfo(lang).Parent.EnglishName;
            return $" Please respond in {name}.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return string.Empty;
        }
    }

    //build a prompt with real paths so the model knows exactly what gets cleaned
    private static string BuildPrompt(CleanerEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Explain what the Winapp2 cleaner entry \"{entry.Name}\" cleans and whether it is safe to delete.");

        if (!string.IsNullOrWhiteSpace(entry.Warning))
            sb.AppendLine($"Warning from the database: {entry.Warning}");

        if (entry.FileKeys.Count > 0)
        {
            sb.AppendLine("It deletes files from these locations:");
            foreach (var fk in entry.FileKeys.Take(6))
                sb.AppendLine($"  - {fk.Path}  (pattern: {fk.Pattern})");
        }

        if (entry.RegKeys.Count > 0)
        {
            sb.AppendLine("It removes these registry keys:");
            foreach (var rk in entry.RegKeys.Take(4))
                sb.AppendLine($"  - {rk.KeyPath}");
        }

        sb.AppendLine("Answer in 2-3 sentences. Be specific and practical.");
        return sb.ToString();
    }
}
