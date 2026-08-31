using System.Security.Cryptography;
using System.Text;

namespace FluentCleaner.Services;

/// <summary>
/// Provides secure storage for sensitive data (such as API keys) using Windows DPAPI (DataProtectionScope.CurrentUser)
/// with a safe fallback for non-Windows environments.
/// </summary>
public static class SecretStore
{
    private static readonly string SecretDir = AppSettings.IsPortable
        ? Path.Combine(AppContext.BaseDirectory, ".secrets")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FluentCleaner", ".secrets");

    public static void SaveSecret(string name, string? secret)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name cannot be empty.", nameof(name));

        var filePath = GetSecretFilePath(name);

        if (string.IsNullOrWhiteSpace(secret))
        {
            DeleteSecret(name);
            return;
        }

        try
        {
            Directory.CreateDirectory(SecretDir);
            var plainBytes = Encoding.UTF8.GetBytes(secret);

            byte[] outputBytes;
            if (OperatingSystem.IsWindows())
            {
                outputBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Fallback for non-Windows (testing/cross-platform)
                outputBytes = plainBytes;
            }

            File.WriteAllBytes(filePath, outputBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretStore] Failed to save secret '{name}': {ex}");
        }
    }

    public static string? LoadSecret(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var filePath = GetSecretFilePath(name);
        if (!File.Exists(filePath))
            return null;

        try
        {
            var inputBytes = File.ReadAllBytes(filePath);

            byte[] plainBytes;
            if (OperatingSystem.IsWindows())
            {
                plainBytes = ProtectedData.Unprotect(inputBytes, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Fallback for non-Windows
                plainBytes = inputBytes;
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or CryptographicException)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretStore] Failed to load secret '{name}': {ex}");
            return null;
        }
    }

    public static void DeleteSecret(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        var filePath = GetSecretFilePath(name);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            System.Diagnostics.Debug.WriteLine($"[SecretStore] Failed to delete secret '{name}': {ex}");
        }
    }

    private static string GetSecretFilePath(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(SecretDir, $"{safeName}.dat");
    }
}
