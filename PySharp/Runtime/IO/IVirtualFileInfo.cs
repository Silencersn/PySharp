namespace PySharp.Runtime.IO;

public interface IVirtualFileInfo : IVirtualFileSystemInfo
{
    IVirtualDirectoryInfo? Directory { get; }

    Stream Open(FileMode mode, FileAccess access, FileShare share);
}
