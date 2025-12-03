using PySharp.PyRuntime.Environments;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.IO.Memory;

public sealed class MemoryFileSystemBuilder
{
    private const string DefaultWorkingDirectory = "C:\\";
    private readonly MemoryFileSystem _fileSystem;

    public MemoryFileSystemBuilder()
    {
        _fileSystem = new MemoryFileSystem(DefaultWorkingDirectory);
    }

    public MemoryFileSystemBuilder WithFile(string name, ReadOnlySpan<char> content)
    {
        _fileSystem.AddFile(name, content);
        return this;
    }
    public MemoryFileSystemBuilder WithWorkingDirectory(string? workingDirectory)
    {
        _fileSystem.CurrentDirectory = workingDirectory ?? DefaultWorkingDirectory;
        return this;
    }
    public MemoryFileSystem Build()
    {
        return _fileSystem;
    }
}
