using System.Runtime.InteropServices;
using System.Text;

namespace FluentCleaner.Services;

/// <summary>
/// Provides secure storage for sensitive data (such as API keys) using Windows DPAPI (CryptProtectData)
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
                outputBytes = ProtectData(plainBytes);
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
                plainBytes = UnprotectData(inputBytes);
            }
            else
            {
                // Fallback for non-Windows
                plainBytes = inputBytes;
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
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

    #region Win32 DPAPI P/Invoke

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    private static byte[] ProtectData(byte[] plainBytes)
    {
        var inBlob = new DATA_BLOB();
        var outBlob = new DATA_BLOB();

        GCHandle pin = GCHandle.Alloc(plainBytes, GCHandleType.Pinned);
        try
        {
            inBlob.cbData = plainBytes.Length;
            inBlob.pbData = pin.AddrOfPinnedObject();

            if (!CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            byte[] result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            if (pin.IsAllocated) pin.Free();
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }

    private static byte[] UnprotectData(byte[] cipherBytes)
    {
        var inBlob = new DATA_BLOB();
        var outBlob = new DATA_BLOB();

        GCHandle pin = GCHandle.Alloc(cipherBytes, GCHandleType.Pinned);
        try
        {
            inBlob.cbData = cipherBytes.Length;
            inBlob.pbData = pin.AddrOfPinnedObject();

            if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            byte[] result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            if (pin.IsAllocated) pin.Free();
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }

    #endregion
}
