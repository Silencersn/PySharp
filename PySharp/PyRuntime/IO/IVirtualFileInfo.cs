using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.IO;

public interface IVirtualFileInfo : IVirtualFileSystemInfo
{
    IVirtualDirectoryInfo? Directory { get; }

    Stream Open(FileMode mode, FileAccess access);
}
