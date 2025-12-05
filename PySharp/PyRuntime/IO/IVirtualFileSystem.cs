using System.Text;

namespace PySharp.PyRuntime.IO;

public interface IVirtualFileSystem
{
    string CurrentDirectory { get; }

    IVirtualDirectoryInfo GetDirectory(string path);
    IVirtualFileInfo GetFile(string fileName);

    bool ExistsDirectory(string? path) => path is not null && GetDirectory(path).Exists;
    bool ExistsFile(string? path) => path is not null && GetFile(path).Exists;

    string ReadAllText(string path)
    {
        var fileInfo = GetFile(path);
        if (!fileInfo.Exists)
            throw new FileNotFoundException();
        using var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    void WriteAllText(string path, ReadOnlySpan<char> contents, Encoding? encoding = null)
    {
        var fileInfo = GetFile(path);
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, encoding);
        writer.Write(contents);
    }

    string GetFullPath(string path);
}