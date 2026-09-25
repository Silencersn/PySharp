# 虚拟文件系统参考

源码：`PySharp/Runtime/IO/`。命名空间：`PySharp.Runtime.IO`，实现在 `.Memory` 与 `.Physical`
子命名空间。

支撑 `open()` 与 `import` 的文件系统抽象。使用指南见
[虚拟文件系统](../user-guide/virtual-file-system.md)。

## IVirtualFileSystem

| 成员 | 说明 |
| --- | --- |
| `string CurrentDirectory { get; }` | 当前工作目录，作为相对路径解析的基准 |
| `PathHelper PathHelper { get; }` | 路径运算器（`Combine`、`GetFullPath`、`GetDirectoryName` 等）；`PathHelper.Default` 采用 .NET `Path` 语义 |
| `IVirtualDirectoryInfo GetDirectory(string path)` | 目录信息 |
| `IVirtualFileInfo GetFile(string fileName)` | 文件信息 |
| `bool ExistsDirectory(string? path)` | 默认实现为 `path is not null && GetDirectory(path).Exists` |
| `bool ExistsFile(string? path)` | 默认实现为 `path is not null && GetFile(path).Exists` |
| `string ReadAllText(string path)` | 默认实现经 `GetFile` 与 `StreamReader`；文件不存在抛 `FileNotFoundException` |
| `byte[] ReadAllBytes(string path)` | 默认实现同上，按字节读取 |
| `void WriteAllText(string path, ReadOnlySpan<char> contents, Encoding? encoding = null)` | 默认实现以 `FileMode.Create` 打开后写入 |
| `string GetFullPath(string path)` | 基于 `CurrentDirectory` 的绝对路径 |

自定义实现只需提供 `CurrentDirectory`、`PathHelper`、`GetDirectory`、`GetFile` 四个核心成员。

## 信息接口

| 接口 | 成员 |
| --- | --- |
| `IVirtualFileSystemInfo` | `Name`、`FullName`、`Exists`、`Create()`、`Delete()` |
| `IVirtualFileInfo` | 继承前者；`Directory : IVirtualDirectoryInfo`、`Open(FileMode, FileAccess, FileShare) : Stream` |
| `IVirtualDirectoryInfo` | 继承前者；`Parent`、`Root`、`EnumerateFiles()`、`EnumerateDirectories()` |

## MemoryFileSystem

`sealed class`，完整的内存文件系统：目录树加按字节存储的文件，支持 `FileShare` 的共享与独占语义，
线程安全（内部全局锁）。

| 成员 | 说明 |
| --- | --- |
| `MemoryFileSystem(string currentDirectory, PathHelper? pathHelper = null)` | 直接构造 |
| `static MemoryFileSystemBuilder CreateBuilder(PathHelper? pathHelper = null)` | 构建器入口 |
| `CurrentDirectory`（可写） | 设置时若非 `null`，会解析为绝对路径并创建该目录 |

`MemoryFileSystemBuilder`（`sealed class`，公共）：

| 成员 | 说明 |
| --- | --- |
| `MemoryFileSystemBuilder(PathHelper? pathHelper = null)` | 构造 |
| `WithFile(string name, ReadOnlySpan<char> content)` | 写入一个文本文件，自动创建父目录 |
| `WithWorkingDirectory(string? workingDirectory)` | 设置当前目录；`null` 时取进程当前目录 |
| `Build() : MemoryFileSystem` | 返回构建好的实例；构建后仍可经接口继续写 |

## PhysicalFileSystem

物理磁盘桥接实现，`PhysicalFileSystem.Shared` 为共享单例。目录与文件信息由
`PhysicalDirectoryInfo`、`PhysicalFileInfo` 等包装。

## 相关节点

- [PyEnvironmentHost 参考](./PyEnvironmentHost.md)：挂载方式
- [配置执行环境](../user-guide/environment.md)：`sys.path` 与 import 解析
