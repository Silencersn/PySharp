# 虚拟文件系统

源码：`PySharp/Runtime/IO/`。

Python 的 `open()` 与 `import` 不直接触碰磁盘，而是经由环境宿主提供的 `IVirtualFileSystem`。
这让脚本可以运行在内存中、沙箱里，或任何自定义的存储之上。

## 接口

```csharp
public interface IVirtualFileSystem
{
    string CurrentDirectory { get; }        // 当前工作目录
    PathHelper PathHelper { get; }          // 路径解析器，分隔符规则可插拔

    IVirtualDirectoryInfo GetDirectory(string path);
    IVirtualFileInfo GetFile(string fileName);

    bool ExistsDirectory(string? path);     // 默认实现基于 GetDirectory
    bool ExistsFile(string? path);          // 默认实现基于 GetFile
    string ReadAllText(string path);        // 默认实现基于 GetFile 与 StreamReader
    byte[] ReadAllBytes(string path);       // 默认实现同上，按字节读取
    void WriteAllText(string path, ReadOnlySpan<char> contents, Encoding? encoding = null);
    string GetFullPath(string path);        // 基于 CurrentDirectory 解析为绝对路径
}
```

`IVirtualFileInfo` 与 `IVirtualDirectoryInfo` 继承 `IVirtualFileSystemInfo`，提供 `Name`、
`FullName`、`Exists`、`Create()`、`Delete()`；文件侧另有 `Open(FileMode, FileAccess, FileShare)`
返回 `Stream`，目录侧另有 `Parent`、`Root`、`EnumerateFiles()`、`EnumerateDirectories()`。
接口自带文本与字节读写的默认实现，自定义文件系统只需实现 `CurrentDirectory`、`PathHelper`、
`GetDirectory`、`GetFile` 四个核心成员。

## 内置实现

| 实现 | 说明 |
| --- | --- |
| `MemoryFileSystem` | 完整的内存文件系统：目录树加文件字节存储，带共享与独占打开语义（`FileShare`），线程安全 |
| `PhysicalFileSystem` | 物理磁盘的桥接实现，`PhysicalFileSystem.Shared` 为共享单例 |

### MemoryFileSystem 构建器

```csharp
using PySharp.Runtime.IO.Memory;

var fs = MemoryFileSystem.CreateBuilder()
    .WithWorkingDirectory("/app")              // 当前目录，默认取进程当前目录
    .WithFile("/app/mymodule.py", "VALUE = 42")
    .WithFile("/app/pkg/__init__.py", "")
    .WithFile("/app/pkg/core.py", "def run():\n    return 'ok'")
    .Build();
```

`WithFile(name, content)` 自动创建父目录。构建后的 `MemoryFileSystem` 仍可通过
`IVirtualFileSystem` 接口继续写入（`WriteAllText`），供运行期脚本创建文件。

### PhysicalFileSystem

`PyInterpreter.RunFile` 内部使用 `PyEnvironmentHost.CreateConsole(usingPhysicalFileSystem: true)`
挂载它。也可以显式组装：

```csharp
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO.Physical;

var host = PyEnvironmentHost.CreateBuilder()
    .UseFileSystem(PhysicalFileSystem.Shared)
    .Build();
```

## 与 import 和 open 的关系

- `import`：`sys.path` 中每个目录会在环境的文件系统中查找 `<name>.py` 与 `<name>/__init__.py`
  （包）。路径模块解析器逐目录扫描，命中后从文件系统读取源码、编译并执行，见
  [配置执行环境](./environment.md)。
- `open()`：相对路径基于文件系统的 `CurrentDirectory` 解析。返回的文件对象的方法面见
  [文件对象](./file-objects.md)。

因此在内存中提供可 `import` 的模块是一等场景：

```csharp
using PySharp.Runtime;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO.Memory;

var fs = MemoryFileSystem.CreateBuilder()
    .WithFile("/lib/greeting.py", "def hello(name):\n    return f'hello, {name}'")
    .Build();

var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(Console.OpenStandardOutput())
    .UseFileSystem(fs)
    .Build();

using var environment = host.CreateEnvironmentBuilder()
    .AddArg("-c")
    .AddPath("/lib")                 // sys.path 指向内存目录
    .Build();

using var interpreter = PyInterpreter.Create(environment);
interpreter.Execute("from greeting import hello\nprint(hello('PySharp'))", "<main>");
// 输出：hello, PySharp
```

这也是嵌入者提供自定义 Python 模块的推荐方式：把模块源码放进内存文件系统即可，无需在 C# 中实现
模块对象。在 C# 侧实现模块的方式见[用 C# 编写 Python 模块](./custom-modules.md)。

## 自定义文件系统

实现 `IVirtualFileSystem` 的四个核心成员，并通过宿主构建器挂载：

```csharp
sealed class DatabaseFileSystem : IVirtualFileSystem
{
    public string CurrentDirectory => "/";
    public PathHelper PathHelper => PathHelper.Default;

    public IVirtualDirectoryInfo GetDirectory(string path) => /* 从数据库读目录 */;
    public IVirtualFileInfo GetFile(string fileName) => /* 从数据库读文件 */;
}

var host = PyEnvironmentHost.CreateBuilder()
    .UseFileSystem(new DatabaseFileSystem())
    .Build();
```

`PathHelper` 封装 `Combine`、`GetFullPath`、`GetDirectoryName` 等路径运算，`PathHelper.Default`
使用 .NET 的 `Path` 语义；需要类 Unix 或其他分隔符规则时可提供自己的实例。

## API 参考

逐成员说明见 [PyFileSystem](../api/PyFileSystem.md)。
