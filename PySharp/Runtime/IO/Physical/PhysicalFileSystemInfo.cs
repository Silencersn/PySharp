namespace PySharp.Runtime.IO.Physical;

public abstract class PhysicalFileSystemInfo : IVirtualFileSystemInfo
{
    internal abstract FileSystemInfo FileSystemInfo { get; }

    public string Name => FileSystemInfo.Name;

    public string FullName => FileSystemInfo.FullName;

    public bool Exists => FileSystemInfo.Exists;

    public abstract void Create();

    public void Delete()
    {
        FileSystemInfo.Delete();
    }
}
