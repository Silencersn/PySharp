namespace PySharp.Runtime.IO.Physical;

public sealed class PhysicalFileInfo : PhysicalFileSystemInfo, IVirtualFileInfo
{
    internal override FileInfo FileSystemInfo { get; }

    public IVirtualDirectoryInfo? Directory => FileSystemInfo.Directory is null ? null : new PhysicalDirectoryInfo(FileSystemInfo.Directory);

    internal PhysicalFileInfo(FileInfo fileInfo)
    {
        FileSystemInfo = fileInfo;
    }

    public override void Create()
    {
        FileSystemInfo.Create();
    }

    public Stream Open(FileMode mode, FileAccess access, FileShare share)
    {
        return FileSystemInfo.Open(mode, access, share);
    }
}
