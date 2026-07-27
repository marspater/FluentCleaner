namespace FluentCleaner.Services;

public static class FileSystem
{
    private static IFileSystemProvider? _provider;
    public static IFileSystemProvider Provider
    {
        get => _provider ??= new DefaultFileSystemProvider();
        set => _provider = value;
    }
}
