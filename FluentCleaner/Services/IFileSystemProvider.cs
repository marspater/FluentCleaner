using System.Collections.Generic;
using System.IO;

namespace FluentCleaner.Services;

public interface IFileSystemProvider
{
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern);
    IEnumerable<string> EnumerateDirectories(string path);
    FileAttributes GetAttributes(string path);
    long GetFileLength(string path);
    long TryGetDeletableSize(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path);
    string[] GetDirectories(string path, string searchPattern, SearchOption searchOption);
    string[] GetFileSystemEntries(string path);
    string[] GetFileSystemEntries(string path, string searchPattern);
}
