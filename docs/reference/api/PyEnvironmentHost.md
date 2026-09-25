# PyEnvironmentHost 参考

源码：`PySharp/Runtime/Environments/PyEnvironmentHost.cs`、`PyEnvironmentHostBuilder.cs`。
命名空间：`PySharp.Runtime.Environments`。

宿主抽象，提供 Python 代码的标准 I/O 与文件系统来源，`abstract class`。标准 I/O 使用 `Stream`
抽象。使用指南见[宿主与 I/O 重定向](../user-guide/hosting.md)。

## 成员

自定义宿主按需覆写以下成员：

| 成员 | 说明 |
| --- | --- |
| `abstract Stream AllocateStdIn()` | 环境构造时调用一次，分配 stdin |
| `abstract Stream AllocateStdOut()` | 分配 stdout |
| `abstract Stream AllocateStdErr()` | 分配 stderr |
| `abstract IVirtualFileSystem FileSystem { get; }` | 文件系统，支撑 `open()` 与 `import` |
| `virtual Encoding DefaultEncoding` | 流包装的默认编码（UTF-8 无 BOM），环境构建器未显式设置编码时生效 |
| `virtual bool SupportsColorOutput` | 向 stdout 输出时是否使用 ANSI 颜色 |
| `virtual bool SupportsErrorColorOutput` | 向 stderr 打印 traceback 时是否使用 ANSI 颜色，默认取 `SupportsColorOutput` |
| `virtual IPyEnvironmentBuilder CreateEnvironmentBuilder()` | 创建绑定本宿主的环境构建器，控制台系宿主覆写它预置 Console 编码 |

## 静态工厂

| 成员 | stdin、stdout、stderr | 文件系统 |
| --- | --- | --- |
| `static CreateNull() : PyEnvironmentHost` | 三个 `Stream.Null` | 空内存 FS |
| `static CreateConsole(bool usingPhysicalFileSystem = false)` | `Console.OpenStandardInput()`、`OpenStandardOutput()`、`OpenStandardError()` | 内存 FS；`true` 时为 `PhysicalFileSystem.Shared` |
| `static CreateRepl()` | 同 `CreateConsole` | 空内存 FS |
| `static CreateBuilder() : IPyEnvironmentHostBuilder` | 可配置（`Stream`） | 可配置 |

## IPyEnvironmentHostBuilder

```csharp
public interface IPyEnvironmentHostBuilder
{
    IPyEnvironmentHostBuilder UseIn(Stream reader);                          // 默认 Stream.Null
    IPyEnvironmentHostBuilder UseOut(Stream writer);                         // 默认 Stream.Null
    IPyEnvironmentHostBuilder UseError(Stream writer);                       // 默认 Stream.Null
    IPyEnvironmentHostBuilder UseFileSystem(IVirtualFileSystem fileSystem);  // 默认空内存 FS
    PyEnvironmentHost Build();
}
```

实现类 `PyEnvironmentHostBuilder` 为 `internal`，仅以接口形式公开。`Build()` 产出的宿主把传入的流
原样作为 `Allocate*` 的返回值。流到读写器的包装发生在环境构造时，编码取 `DefaultEncoding` 或环境
构建器的 `UseStd*Encoding`，stdout 与 stderr 的 `AutoFlush` 为 `true`。

## 相关节点

- [PyFileSystem 参考](./PyFileSystem.md)
- [PyEnvironment 参考](./PyEnvironment.md)
