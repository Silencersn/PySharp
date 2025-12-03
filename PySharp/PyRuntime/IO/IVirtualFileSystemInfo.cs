using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.IO;

public interface IVirtualFileSystemInfo
{
    string Name { get; }
    string FullName { get; }
    bool Exists { get; }

    void Create();
    void Delete();
}
