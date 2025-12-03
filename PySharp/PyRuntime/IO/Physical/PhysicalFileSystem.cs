using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PySharp.PyRuntime.IO.Physical;

public sealed class PhysicalFileSystem : IVirtualFileSystem
{
    public static PhysicalFileSystem Shared { get; } = new PhysicalFileSystem();

    public string CurrentDirectory => Environment.CurrentDirectory;

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
