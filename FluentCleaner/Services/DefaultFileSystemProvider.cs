using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FluentCleaner.Services;

public class DefaultFileSystemProvider : IFileSystemProvider
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => Directory.EnumerateFiles(path, searchPattern);

    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public long TryGetDeletableSize(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { try { return new FileInfo(path).Length; } catch { return -1; } }

        const uint DELETE = 0x00010000;
        const uint FILE_SHARE_ALL = 0x7;   // Read | Write | Delete
        const uint OPEN_EXISTING = 3;

        using var handle = CreateFileW(path, DELETE, FILE_SHARE_ALL,
                                       IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (handle.IsInvalid) return -1; try { return new FileInfo(path).Length; } catch { return -1; }
    }

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDirectory(string path) => Directory.Delete(path);

    public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => Directory.GetDirectories(path, searchPattern, searchOption);

    public string[] GetFileSystemEntries(string path) => Directory.GetFileSystemEntries(path);

    public string[] GetFileSystemEntries(string path, string searchPattern) => Directory.GetFileSystemEntries(path, searchPattern);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);
}
