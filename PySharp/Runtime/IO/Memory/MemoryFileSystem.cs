using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.IO.Memory;

public sealed class MemoryFileSystem : IVirtualFileSystem
{
    internal readonly PathHelper Path;
    internal readonly Lock SyncRoot = new();

    internal readonly HashSet<string> _roots;
    internal readonly Dictionary<string, HashSet<string>> _directoryToSubdirectories;
    internal readonly Dictionary<string, HashSet<string>> _directoryToFiles;
    internal readonly Dictionary<string, MemoryFileData> _files;

    public string CurrentDirectory
    {
        get;
        internal set
        {
            field = field is null ? value : Path.GetFullPath(value, field);
            GetDirectory(field).Create();
        }
    }

    public PathHelper PathHelper => Path;

    public MemoryFileSystem(string currentDirectory, PathHelper? pathHelper = null)
    {
        Path = pathHelper ?? PathHelper.Default;
        _roots = [];
        _directoryToSubdirectories = [];
        _directoryToFiles = [];
        _files = [];
        CurrentDirectory = currentDirectory;
    }

    public static MemoryFileSystemBuilder CreateBuilder(PathHelper? pathHelper = null)
    {
        return new MemoryFileSystemBuilder(pathHelper);
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
        lock (SyncRoot)
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
    }

    internal MemoryFileData InternalCreateFile(string fullName)
    {
        lock (SyncRoot)
        {
            var directory = Path.GetDirectoryName(fullName);
            Debug.Assert(directory is not null);
            if (!InternalExistsDirectory(directory))
                throw new DirectoryNotFoundException(directory);
            _directoryToFiles[directory].Add(fullName);
            return _files[fullName] = new MemoryFileData();
        }
    }

    internal void InternalDeleteDirectory(string fullPath)
    {
        lock (SyncRoot)
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

            var removed = _directoryToFiles.Remove(fullPath);
            Debug.Assert(removed);

            removed = _directoryToSubdirectories.Remove(fullPath);
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
    }

    internal void InternalDeleteFile(string fullName)
    {
        lock (SyncRoot)
        {
            var directory = Path.GetDirectoryName(fullName);
            Debug.Assert(directory is not null);
            if (!InternalExistsDirectory(directory))
                return;
            _directoryToFiles[directory].Remove(fullName);
            if (!_files.Remove(fullName))
                throw new UnreachableException();
        }
    }

    internal bool InternalExistsDirectory(string fullPath)
    {
        lock (SyncRoot)
            return _directoryToSubdirectories.ContainsKey(fullPath);
    }

    internal bool InternalTryGetFile(string fullName, [NotNullWhen(true)] out MemoryFileData? data)
    {
        lock (SyncRoot)
            return _files.TryGetValue(fullName, out data);
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

        lock (SyncRoot)
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
        Name = _owner.Path.GetFileName(fullPath);
        _parentPath = _owner.Path.GetDirectoryName(fullPath);
    }

    public override string Name { get; }

    public override string FullName { get; }

    public override bool Exists => _owner.ExistsDirectory(FullName);

    public IVirtualDirectoryInfo? Parent => _parentPath is null ? null : _owner.GetDirectory(_parentPath);

    public IVirtualDirectoryInfo? Root => _owner.Path.GetPathRoot(FullName) is string { Length: > 0 } root ? _owner.GetDirectory(root) : null;

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
        lock (_owner.SyncRoot)
        {
            if (!Exists)
                throw new DirectoryNotFoundException(FullName);

            return [.. _owner._directoryToSubdirectories[FullName].Select(_owner.GetDirectory)];
        }
    }

    public IEnumerable<IVirtualFileInfo> EnumerateFiles()
    {
        lock (_owner.SyncRoot)
        {
            if (!Exists)
                throw new DirectoryNotFoundException(FullName);

            return [.. _owner._directoryToFiles[FullName].Select(_owner.GetFile)];
        }
    }
}

public sealed class MemoryFileInfo : MemoryFileSystemInfo, IVirtualFileInfo
{
    private readonly MemoryFileSystem _owner;

    internal MemoryFileInfo(MemoryFileSystem owner, string fullPath)
    {
        _owner = owner;
        FullName = fullPath;
        Name = _owner.Path.GetFileName(fullPath);
    }

    public override string Name { get; }

    public override string FullName { get; }

    public override bool Exists => _owner.ExistsFile(FullName);

    public IVirtualDirectoryInfo Directory => _owner.GetDirectory(_owner.Path.GetDirectoryName(FullName)!);

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
        MemoryFileData? data;
        switch (mode)
        {
            case FileMode.Open:
                if (!_owner.InternalTryGetFile(FullName, out data))
                    throw new FileNotFoundException(FullName);
                break;

            case FileMode.Create or FileMode.OpenOrCreate:
                if (!_owner.InternalTryGetFile(FullName, out data))
                    data = _owner.InternalCreateFile(FullName);
                break;

            case FileMode.CreateNew:
                if (_owner.InternalTryGetFile(FullName, out _))
                    throw new IOException(FullName);
                data = _owner.InternalCreateFile(FullName);
                break;

            case FileMode.Truncate:
                if (!_owner.InternalTryGetFile(FullName, out data))
                    throw new FileNotFoundException(FullName);
                break;

            case FileMode.Append:
                EnsureWriteAccess();
                if (!_owner.InternalTryGetFile(FullName, out data))
                    data = _owner.InternalCreateFile(FullName);
                break;

            default:
                throw new ArgumentException(null, nameof(mode));
        }

        if (!data.TryAccess(access, share, out var stream))
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

internal sealed class MemoryFileData
{
    private byte[] _data = [];
    private readonly Lock _lock = new();
    private readonly List<MemoryFileStream> _streams = [];

    public bool TryAccess(FileAccess access, FileShare share, [NotNullWhen(true)] out MemoryFileStream? stream)
    {
        stream = null;

        lock (_lock)
        {
            foreach (var existing in _streams)
            {
                if (access.HasFlag(FileAccess.Write))
                {
                    if (!existing.Share.HasFlag(FileShare.Write))
                        return false;

                    if (existing.Access.HasFlag(FileAccess.Write))
                        return false;
                }

                if (access.HasFlag(FileAccess.Read) && !existing.Share.HasFlag(FileShare.Read))
                    return false;
            }

            stream = new MemoryFileStream(this, access, share);
            _streams.Add(stream);
            return true;
        }
    }

    public void Release(MemoryFileStream stream)
    {
        lock (_lock)
            _streams.Remove(stream);
    }

    public int Read(long position, byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (position >= _data.Length)
                return 0;
            int available = _data.Length - (int)position;
            int toRead = Math.Min(available, count);
            Array.Copy(_data, position, buffer, offset, toRead);
            return toRead;
        }
    }

    public void Write(long position, byte[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (position + count > _data.Length)
                Array.Resize(ref _data, (int)(position + count));
            Array.Copy(buffer, offset, _data, position, count);
        }
    }

    public void SetLength(long length)
    {
        lock (_lock)
            Array.Resize(ref _data, (int)length);
    }

    public long Length
    {
        get
        {
            lock (_lock)
                return _data.Length;
        }
    }
}

internal sealed class MemoryFileStream : Stream
{
    private MemoryFileData? _data;
    private long _position;
    private readonly FileAccess _access;
    private readonly FileShare _share;

    public MemoryFileStream(MemoryFileData data, FileAccess access, FileShare share)
    {
        _data = data;
        _access = access;
        _share = share;
    }

    public override bool CanRead => _access.HasFlag(FileAccess.Read);

    public override bool CanSeek => true;

    public override bool CanWrite => _access.HasFlag(FileAccess.Write);

    public override long Length => _data?.Length ?? 0;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    internal FileAccess Access => _access;

    internal FileShare Share => _share;

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

    public override void Flush()
    {
        // do nothing
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureReadable();
        ObjectDisposedException.ThrowIf(_data is null, typeof(MemoryFileStream));
        int read = _data.Read(_position, buffer, offset, count);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_data is null, typeof(MemoryFileStream));
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _data.Length + offset,
            _ => throw new ArgumentException(null, nameof(origin))
        };
        return _position;
    }

    public override void SetLength(long value)
    {
        EnsureWritable();
        ObjectDisposedException.ThrowIf(_data is null, typeof(MemoryFileStream));
        _data.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureWritable();
        ObjectDisposedException.ThrowIf(_data is null, typeof(MemoryFileStream));
        _data.Write(_position, buffer, offset, count);
        _position += count;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _data is not null)
        {
            _data.Release(this);
            _data = null;
        }
        base.Dispose(disposing);
    }
}
