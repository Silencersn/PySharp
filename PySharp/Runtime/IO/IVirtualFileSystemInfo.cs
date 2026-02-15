namespace PySharp.Runtime.IO;

public interface IVirtualFileSystemInfo
{
    string Name { get; }
    string FullName { get; }
    bool Exists { get; }

    void Create();
    void Delete();
}
