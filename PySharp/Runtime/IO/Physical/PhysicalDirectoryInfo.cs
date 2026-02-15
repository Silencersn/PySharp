namespace PySharp.Runtime.IO.Physical;

public sealed class PhysicalDirectoryInfo : PhysicalFileSystemInfo, IVirtualDirectoryInfo
{
    internal override DirectoryInfo FileSystemInfo { get; }

    public IVirtualDirectoryInfo? Parent => FileSystemInfo.Parent is null ? null : new PhysicalDirectoryInfo(FileSystemInfo.Parent);

    public IVirtualDirectoryInfo Root => new PhysicalDirectoryInfo(FileSystemInfo.Root);

    public PhysicalDirectoryInfo(DirectoryInfo directoryInfo)
    {
        FileSystemInfo = directoryInfo;
    }

    public override void Create()
    {
        FileSystemInfo.Create();
    }

    public IEnumerable<IVirtualDirectoryInfo> EnumerateDirectories()
    {
        return FileSystemInfo.EnumerateDirectories().Select(directoryInfo => new PhysicalDirectoryInfo(directoryInfo));
    }

    public IEnumerable<IVirtualFileInfo> EnumerateFiles()
    {
        return FileSystemInfo.EnumerateFiles().Select(fileInfo => new PhysicalFileInfo(fileInfo));
    }
}
