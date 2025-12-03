using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace PySharp.PyRuntime.IO.Memory;

internal sealed class ConcurrentSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dict = [];

    public bool Add(T item)
    {
        return _dict.TryAdd(item, default);
    }

    public bool Remove(T item)
    {
        return _dict.TryRemove(item, out _);
    }

    public bool Contains(T item)
    {
        return _dict.ContainsKey(item);
    }

    public void Clear()
    {
        _dict.Clear();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _dict.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public sealed class MemoryFileSystem : IVirtualFileSystem
{
    internal readonly ConcurrentDictionary<string, ConcurrentSet<string>> _directoryToSubdirectories;
    internal readonly ConcurrentDictionary<string, ConcurrentSet<string>> _directoryToFiles;
    internal readonly ConcurrentDictionary<string, MemoryStream> _files;

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
        _directoryToSubdirectories[fullPath] = [];
        _directoryToFiles[fullPath] = [];
    }

    internal void InternalCreateFile(string fullName)
    {
        var directory = Path.GetDirectoryName(fullName);
        Debug.Assert(directory is not null);
        if (!InternalExistsDirectory(directory))
            throw new DirectoryNotFoundException(directory);
        _directoryToFiles[directory].Add(fullName);
        if (_files.TryRemove(fullName, out var stream))
            stream.Dispose();
        _files[fullName] = new MemoryStream();
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
            _directoryToSubdirectories[parentPath].Remove(fullPath);
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

    internal bool InternalTryGetFile(string fullName, [NotNullWhen(true)] out MemoryStream? stream)
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

    public Stream Open(FileMode mode, FileAccess access)
    {
        MemoryStream? stream;
        switch (mode)
        {
            case FileMode.Open when access is FileAccess.Read:
                if (!_owner.InternalTryGetFile(FullName, out stream))
                    throw new FileNotFoundException();
                stream.Seek(0, SeekOrigin.Begin);
                return new MemoryWrapperStream(stream, true, false);

            case FileMode.Create when access is FileAccess.Write:
                _owner.InternalCreateFile(FullName);
                if (!_owner.InternalTryGetFile(FullName, out stream))
                    Debug.Fail(null);
                return new MemoryWrapperStream(stream, false, true);
        }

        throw new NotImplementedException();
    }
}


internal class MemoryWrapperStream : Stream
{
    private readonly MemoryStream _memoryStream;
    private readonly bool _canRead;
    private readonly bool _canWrite;

    internal MemoryWrapperStream(MemoryStream memoryStream, bool canRead, bool canWrite)
    {
        _memoryStream = memoryStream;
        _canRead = canRead;
        _canWrite = canWrite;
    }

    public override bool CanRead => _canRead && _memoryStream.CanRead;

    public override bool CanWrite => _canWrite && _memoryStream.CanWrite;

    public override bool CanSeek => _memoryStream.CanSeek;

    public override long Length => _memoryStream.Length;

    public override long Position
    {
        get => _memoryStream.Position;
        set => _memoryStream.Position = value;
    }

    public override void Flush()
    {
        if (!CanWrite)
            throw new NotSupportedException("Stream is not writable");
        _memoryStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!CanRead)
            throw new NotSupportedException("Stream is not readable");
        return _memoryStream.Read(buffer, offset, count);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!CanWrite)
            throw new NotSupportedException("Stream is not writable");
        _memoryStream.Write(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _memoryStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        if (!CanWrite)
            throw new NotSupportedException("Stream is not writable");
        _memoryStream.SetLength(value);
    }
}