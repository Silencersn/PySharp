using PySharp.Utility;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.IO.Memory;

public sealed class MemoryFileSystem : IVirtualFileSystem
{
    internal readonly ConcurrentSet<string> _roots;
    internal readonly ConcurrentDictionary<string, ConcurrentSet<string>> _directoryToSubdirectories;
    internal readonly ConcurrentDictionary<string, ConcurrentSet<string>> _directoryToFiles;
    internal readonly ConcurrentDictionary<string, MemoryFileStream> _files;

    public string CurrentDirectory
    {
        get;
        internal set
        {
            field = Path.GetFullPath(value);
            GetDirectory(field).Create();
        }
    }

    public MemoryFileSystem(string currentDirectory)
    {
        _roots = [];
        _directoryToSubdirectories = [];
        _directoryToFiles = [];
        _files = [];
        CurrentDirectory = currentDirectory;
    }

    public static MemoryFileSystemBuilder CreateBuilder()
    {
        return new MemoryFileSystemBuilder();
    }

    public IVirtualDirectoryInfo GetDirectory(string path)
    {
        return new MemoryDirectoryInfo(this, GetFullPath(path));
    }

    public IVirtualFileInfo GetFile(string fileName)
    {
        return new MemoryFileInfo(this, GetFullPath(fileName));
    }

    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path, CurrentDirectory);
    }

    internal void AddFile(string file, ReadOnlySpan<char> contents)
    {
        GetDirectory(Path.GetDirectoryName(file)!).Create();
        (this as IVirtualFileSystem).WriteAllText(file, contents);
    }

    internal void InternalCreateDirectory(string fullPath)
    {
        if (InternalExistsDirectory(fullPath))
            return;

        var parentPath = Path.GetDirectoryName(fullPath);
        if (parentPath is not null)
        {
            InternalCreateDirectory(parentPath);
            _directoryToSubdirectories[parentPath].Add(fullPath);
        }
        else
        {
            _roots.Add(fullPath);
        }
        _directoryToSubdirectories[fullPath] = [];
        _directoryToFiles[fullPath] = [];
    }

    internal MemoryFileStream InternalCreateFile(string fullName)
    {
        var directory = Path.GetDirectoryName(fullName);
        Debug.Assert(directory is not null);
        if (!InternalExistsDirectory(directory))
            throw new DirectoryNotFoundException(directory);
        _directoryToFiles[directory].Add(fullName);
        if (_files.TryRemove(fullName, out var stream))
            stream.Dispose();
        return _files[fullName] = new MemoryFileStream();
    }

    internal void InternalDeleteDirectory(string fullPath)
    {
        if (!InternalExistsDirectory(fullPath))
            return;

        foreach (var subdirectory in _directoryToSubdirectories[fullPath].ToArray())
        {
            InternalDeleteDirectory(subdirectory);
        }

        foreach (var file in _directoryToFiles[fullPath].ToArray())
        {
            InternalDeleteFile(file);
        }

        var removed = _directoryToFiles.TryRemove(fullPath, out _);
        Debug.Assert(removed);

        removed = _directoryToSubdirectories.TryRemove(fullPath, out _);
        Debug.Assert(removed);

        var parentPath = Path.GetDirectoryName(fullPath);
        if (parentPath is not null)
        {
            _directoryToSubdirectories[parentPath].Remove(fullPath);
        }
        else
        {
            removed = _roots.Remove(fullPath);
            Debug.Assert(removed);
        }
    }

    internal void InternalDeleteFile(string fullName)
    {
        var directory = Path.GetDirectoryName(fullName);
        Debug.Assert(directory is not null);
        if (!InternalExistsDirectory(directory))
            return;
        _directoryToFiles[directory].Remove(fullName);
        if (_files.TryRemove(fullName, out var stream))
            stream.Dispose();
        else
            Debug.Fail(null);
    }

    internal bool InternalExistsDirectory(string fullPath)
    {
        return _directoryToSubdirectories.ContainsKey(fullPath);
    }

    internal bool InternalTryGetFile(string fullName, [NotNullWhen(true)] out MemoryFileStream? stream)
    {
        return _files.TryGetValue(fullName, out stream);
    }

    public bool ExistsDirectory(string? path)
    {
        if (path is null)
            return false;

        return InternalExistsDirectory(GetFullPath(path));
    }

    public bool ExistsFile(string? path)
    {
        if (path is null)
            return false;

        return _files.ContainsKey(GetFullPath(path));
    }
}

public abstract class MemoryFileSystemInfo : IVirtualFileSystemInfo
{
    public abstract string Name { get; }
    public abstract string FullName { get; }
    public abstract bool Exists { get; }

    public abstract void Create();
    public abstract void Delete();
}

public sealed class MemoryDirectoryInfo : MemoryFileSystemInfo, IVirtualDirectoryInfo
{
    private readonly MemoryFileSystem _owner;
    private readonly string? _parentPath;

    internal MemoryDirectoryInfo(MemoryFileSystem owner, string fullPath)
    {
        _owner = owner;
        FullName = fullPath;
        Name = Path.GetFileName(fullPath);
        _parentPath = Path.GetDirectoryName(fullPath);
    }

    public override string Name { get; }

    public override string FullName { get; }

    public override bool Exists => _owner.ExistsDirectory(FullName);

    public IVirtualDirectoryInfo? Parent => _parentPath is null ? null : _owner.GetDirectory(_parentPath);

    public IVirtualDirectoryInfo Root => _owner.GetDirectory(Path.GetPathRoot(FullName)!);

    public override void Create()
    {
        _owner.InternalCreateDirectory(FullName);
    }

    public override void Delete()
    {
        _owner.InternalDeleteDirectory(FullName);
    }

    public IEnumerable<IVirtualDirectoryInfo> EnumerateDirectories()
    {
        if (!Exists)
            throw new DirectoryNotFoundException();

        return _owner._directoryToSubdirectories[FullName].Select(_owner.GetDirectory);
    }

    public IEnumerable<IVirtualFileInfo> EnumerateFiles()
    {
        if (!Exists)
            throw new DirectoryNotFoundException();

        return _owner._directoryToFiles[FullName].Select(_owner.GetFile);
    }
}

public sealed class MemoryFileInfo : MemoryFileSystemInfo, IVirtualFileInfo
{
    private readonly MemoryFileSystem _owner;

    internal MemoryFileInfo(MemoryFileSystem owner, string fullPath)
    {
        _owner = owner;
        FullName = fullPath;
        Name = Path.GetFileName(fullPath);
    }

    public override string Name { get; }

    public override string FullName { get; }

    public override bool Exists => _owner.ExistsFile(FullName);

    public IVirtualDirectoryInfo Directory => _owner.GetDirectory(Path.GetDirectoryName(FullName)!);

    public override void Create()
    {
        _owner.InternalCreateFile(FullName);
    }

    public override void Delete()
    {
        _owner.InternalDeleteFile(FullName);
    }

    public Stream Open(FileMode mode, FileAccess access, FileShare share)
    {
        MemoryFileStream? stream;
        switch (mode)
        {
            case FileMode.Open:
                if (!_owner.InternalTryGetFile(FullName, out stream))
                    throw new FileNotFoundException(FullName);
                break;

            case FileMode.Create or FileMode.OpenOrCreate:
                if (!_owner.InternalTryGetFile(FullName, out stream))
                    stream = _owner.InternalCreateFile(FullName);
                break;

            case FileMode.CreateNew:
                if (_owner.InternalTryGetFile(FullName, out _))
                    throw new IOException(FullName);
                stream = _owner.InternalCreateFile(FullName);
                break;

            case FileMode.Truncate:
                if (!_owner.InternalTryGetFile(FullName, out stream))
                    throw new FileNotFoundException(FullName);
                break;

            case FileMode.Append:
                EnsureWriteAccess();
                if (!_owner.InternalTryGetFile(FullName, out stream))
                    stream = _owner.InternalCreateFile(FullName);
                break;

            default:
                throw new ArgumentException(null, nameof(mode));
        }

        if (!stream.TryAccess(access, share))
            throw new IOException(FullName);

        switch (mode)
        {
            case FileMode.Append:
                stream.Seek(0, SeekOrigin.End);
                break;

            case FileMode.Open:
            case FileMode.OpenOrCreate:
            case FileMode.Create:
            case FileMode.CreateNew:
                stream.Seek(0, SeekOrigin.Begin);
                break;

            case FileMode.Truncate:
                stream.SetLength(0);
                break;
        }

        return stream;

        void EnsureWriteAccess()
        {
            if (!access.HasFlag(FileAccess.Write))
                throw new ArgumentException(null, nameof(access));
        }
    }
}

internal sealed class MemoryFileStream : Stream
{
    private MemoryStream? _stream;
    private int _sharingCount;

    internal MemoryStream Stream => _stream ??= new();
    internal bool IsReadOnly { get; set; }
    internal bool CanDelete { get; set; } = true;
    internal FileAccess Access { get; set; }
    internal FileShare Share { get; set; }

    public override bool CanRead => Access.HasFlag(FileAccess.Read);

    public override bool CanSeek => true;

    public override bool CanWrite => !IsReadOnly && Access.HasFlag(FileAccess.Write);

    public override long Length => _stream?.Length ?? 0;

    public override long Position
    {
        get => _stream?.Position ?? 0;
        set => Stream.Position = value;
    }

    private void EnsureReadable()
    {
        if (!CanRead)
            throw new NotSupportedException("Stream does not support reading");
    }

    private void EnsureWritable()
    {
        if (!CanWrite)
            throw new NotSupportedException("Stream does not support writing");
    }

    internal bool TryAccess(FileAccess access, FileShare share)
    {
        lock (this)
        {
            if (_sharingCount is 0)
            {
                Access = access;
                Share = share;
                _sharingCount++;
                return true;
            }

            if (!share.HasFlag(Share))
                return false;

            // allows multiple readers or one writer (and reader)
            if (Access.HasFlag(FileAccess.Write))
                return false;

            Debug.Assert(Access is FileAccess.Read);
            if (access is FileAccess.Read)
            {
                _sharingCount++;
                return true;
            }

            return false;
        }
    }

    public override void Flush()
    {
        // do nothing, the same as MemoryStream
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureReadable();
        return Stream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return Stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        EnsureWritable();
        Stream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureWritable();
        Stream.Write(buffer, offset, count);
    }

    public override void Close()
    {
        if (_sharingCount is 0)
            return;

        lock (this)
        {
            if (_sharingCount is 0)
                return;

            if (Access.HasFlag(FileAccess.Write))
            {
                Debug.Assert(_sharingCount is 1);
                _sharingCount = 0;
                Access = default;
                Share = default;
            }
            else // Access is FileAccess.Read
            {
                _sharingCount--;
                if (_sharingCount is 0)
                {
                    Access = default;
                    Share = default;
                }
            }
        }
    }
}
