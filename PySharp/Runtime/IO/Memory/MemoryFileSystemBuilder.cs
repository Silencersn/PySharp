namespace PySharp.Runtime.IO.Memory;

public sealed class MemoryFileSystemBuilder
{
    private readonly MemoryFileSystem _fileSystem;
    private readonly PathHelper? _pathHelper;

    public MemoryFileSystemBuilder(PathHelper? pathHelper = null)
    {
        _pathHelper = pathHelper;
        _fileSystem = new MemoryFileSystem(Environment.CurrentDirectory, _pathHelper);
    }

    public MemoryFileSystemBuilder WithFile(string name, ReadOnlySpan<char> content)
    {
        _fileSystem.AddFile(name, content);
        return this;
    }
    public MemoryFileSystemBuilder WithWorkingDirectory(string? workingDirectory)
    {
        _fileSystem.CurrentDirectory = workingDirectory ?? Environment.CurrentDirectory;
        return this;
    }
    public MemoryFileSystem Build()
    {
        return _fileSystem;
    }
}
