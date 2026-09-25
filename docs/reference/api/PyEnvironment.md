# PyEnvironment 参考

源码：`PySharp/Runtime/Environments/PyEnvironment.cs`、`PyEnvironmentBuilder.cs`。
命名空间：`PySharp.Runtime.Environments`。

一次 Python 会话的状态容器，`sealed class`，实现 `IDisposable`。经宿主的构建器创建，
`PyEnvironment` 自身不提供创建入口。使用指南见[配置执行环境](../user-guide/environment.md)。

## PyEnvironment

| 成员 | 说明 |
| --- | --- |
| `PyEnvironmentHost Host { get; }` | 构造时传入的宿主（I/O 与文件系统来源） |
| `PyStrObject.InternPool InternPool { get; }` | 环境级字符串驻留池 |
| `bool OutSupportsColor { get; }` / `bool ErrorSupportsColor { get; }` | 标准输出与错误输出是否支持 ANSI 颜色 |
| `static PyEnvironment CreateNull()` | 空宿主（`Stream.Null` 三流加空内存文件系统）的最小环境 |
| `static PyEnvironment CreateConsole()` | 控制台宿主加内存文件系统的便捷环境 |
| `void Dispose()` | 退出流程：中断并等待全部登记线程，随后释放三条标准流；幂等 |

构造函数为 `internal`。模块缓存、`sys.path` 与 `sys.argv` 列表、线程集合以及 `ExitCode`
等状态成员同为 `internal`，由运行时维护。

## IPyEnvironmentBuilder

```csharp
public interface IPyEnvironmentBuilder
{
    IPyEnvironmentBuilder SetInteractive(bool isInteractive);
    IPyEnvironmentBuilder AddPath(string path);
    IPyEnvironmentBuilder AddArg(string arg);
    IPyEnvironmentBuilder AddArgs(IEnumerable<string>? args);   // 接口默认实现：null 时空操作
    IPyEnvironmentBuilder UseStdInEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdOutEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdErrEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdioEncoding(Encoding encoding);  // 三流一次设置
    IPyEnvironmentBuilder UseStdOutColorSupport(bool enabled);
    IPyEnvironmentBuilder UseStdErrColorSupport(bool enabled);
    IPyEnvironmentBuilder SetOptimizationLevel(int level);
    IPyEnvironmentBuilder NotImplyImportSite();
    PyEnvironment Build();
}
```

构建器由宿主创建，即 `host.CreateEnvironmentBuilder()`。宿主会把默认流编码预置进构建器，
见 [PyEnvironmentHost 参考](./PyEnvironmentHost.md)。实现类 `PyEnvironmentBuilder` 为 `internal`。

## PyEnvironmentOptions

```csharp
public sealed record class PyEnvironmentOptions
{
    public static PyEnvironmentOptions Default { get; }   // NotImplyImportSite = false, OptimizationLevel = 0
    public bool NotImplyImportSite { get; init; }
    public int OptimizationLevel { get; init; }
}
```

`OptimizationLevel` 大于 0 时不发射 `assert` 且 `__debug__` 为 `False`，大于等于 2 时连 docstring
一并去除，对应命令行的 `-O` 与 `-OO`。`NotImplyImportSite` 对应 `-S`，为 `true` 时初始化期间不执行
`import site`。

## 退出码

`sys.exit` 的退出码由解释器解析后记入环境的 `ExitCode`（`internal`），由顶层运行路径与 CLI 消费。
宿主需要"环境退出即进程退出"的语义时，可在 `Dispose` 后读取自行决定。

## 相关节点

- [PyInterpreter 参考](./PyInterpreter.md)、[PyEnvironmentHost 参考](./PyEnvironmentHost.md)
