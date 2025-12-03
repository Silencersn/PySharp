using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.IO;

public interface IVirtualDirectoryInfo : IVirtualFileSystemInfo
{
    IVirtualDirectoryInfo? Parent { get; }
    IVirtualDirectoryInfo Root { get; }

    IEnumerable<IVirtualDirectoryInfo> EnumerateDirectories();
    IEnumerable<IVirtualFileInfo> EnumerateFiles();
}
