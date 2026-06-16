namespace PySharp.Runtime.IO.Physical;

public sealed class PhysicalFileSystem : IVirtualFileSystem
{
    public static PhysicalFileSystem Shared { get; } = new PhysicalFileSystem();

    public string CurrentDirectory => Environment.CurrentDirectory;
    public PathHelper PathHelper => PathHelper.Default;

    private PhysicalFileSystem()
    {
    }

    public IVirtualDirectoryInfo GetDirectory(string path)
    {
        return new PhysicalDirectoryInfo(new DirectoryInfo(path));
    }

    public IVirtualFileInfo GetFile(string fileName)
    {
        return new PhysicalFileInfo(new FileInfo(fileName));
    }

    public bool ExistsDirectory(string? path)
    {
        return Directory.Exists(path);
    }

    public bool ExistsFile(string? path)
    {
        return File.Exists(path);
    }

    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }
}
