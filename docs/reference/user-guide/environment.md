# 配置执行环境

源码：`PySharp/Runtime/Environments/PyEnvironment.cs`、`PyEnvironmentBuilder.cs`。

`PyEnvironment` 表示一次 Python 会话的状态：标准 I/O、模块搜索路径（`sys.path`）、程序参数
（`sys.argv`）、模块缓存、字符串驻留池（`InternPool`）与已登记的 Python 线程集合等。
它通过构建器创建：

```csharp
using PySharp.Runtime.Environments;

var host = PyEnvironmentHost.CreateConsole();          // 先有宿主，见 hosting.md

using var environment = host.CreateEnvironmentBuilder()
    .SetInteractive(false)
    .AddPath("/scripts")
    .AddArg("-c")
    .AddArgs(["a", "b"])
    .Build();
```

`CreateEnvironmentBuilder` 返回公共接口 `IPyEnvironmentBuilder`，配置方法均链式返回自身。

## 构建器选项

| 方法 | 作用 |
| --- | --- |
| `SetInteractive(bool)` | 是否为交互式会话，影响 REPL 行为 |
| `AddPath(string)` | 向模块搜索路径追加一个目录（`sys.path`），可多次调用，按添加顺序排列 |
| `AddArg(string)` | 向 `sys.argv` 追加一个参数 |
| `AddArgs(IEnumerable<string>?)` | 批量追加参数；`null` 时为空操作（接口默认实现） |
| `UseStdInEncoding(Encoding)`、`UseStdOutEncoding(Encoding)`、`UseStdErrEncoding(Encoding)` | 分别设置三条标准流的编码 |
| `UseStdioEncoding(Encoding)` | 一次设置三条标准流的编码 |
| `UseStdOutColorSupport(bool)`、`UseStdErrColorSupport(bool)` | 是否在标准输出与错误输出上使用 ANSI 颜色 |
| `SetOptimizationLevel(int)` | 设置优化级别，对应命令行 `-O` 与 `-OO` |
| `NotImplyImportSite()` | 初始化期间不隐式执行 `import site` |
| `Build()` | 构造 `PyEnvironment` |

REPL 的构建方式是交互标志加 REPL 宿主：

```csharp
var environment = PyEnvironmentHost.CreateRepl().CreateEnvironmentBuilder()
    .SetInteractive(true)
    .Build();
```

`sys.argv` 的首元素按 CPython 惯例由调用方填入（脚本路径或 `"-c"`），`AddArg` 与 `AddArgs`
不做特殊处理，按顺序拼接。

## PyEnvironment 的公共成员

`PyEnvironment` 的公共面很小，大部分状态供运行时内部使用：

| 成员 | 说明 |
| --- | --- |
| `PyEnvironmentHost Host { get; }` | 创建时传入的宿主，即 I/O 与文件系统的来源 |
| `PyStrObject.InternPool InternPool { get; }` | 环境级字符串驻留池 |
| `bool OutSupportsColor { get; }`、`bool ErrorSupportsColor { get; }` | 标准输出与错误输出是否支持 ANSI 颜色 |
| `static CreateNull()` | 空宿主（`Stream.Null` 三流加空内存文件系统）的最小环境 |
| `static CreateConsole()` | 控制台宿主的便捷环境，等价于 `PyEnvironmentHost.CreateConsole().CreateEnvironmentBuilder().Build()` |
| `Dispose()` | 执行退出流程并释放资源 |

## 释放与退出流程

`Dispose` 会中断（`Interrupt`）并等待（`Join`）环境中登记的所有 Python 线程，即 `threading` 模块
创建的线程，最后释放三条标准流。`sys.exit` 的退出码由解释器记录在环境的 `ExitCode`（`internal`），
由顶层运行路径与 CLI 决定进程行为，环境退出不会直接结束进程。

## sys.path 与 import

搜索路径按以下顺序解析模块，均未命中时抛 `ModuleNotFoundError`：

1. 内建模块：`PyStandardLibrary` 注册表中的模块（`builtins`、`sys`、`math` 等）。
2. 路径模块：依次扫描 `sys.path` 中的目录，寻找 `<name>.py` 文件或含 `__init__.py` 的 `<name>/`
   包目录，内容读取自环境的虚拟文件系统。

搜索路径指向的目录由宿主的文件系统解释（内存 FS 或物理 FS），见
[虚拟文件系统](./virtual-file-system.md)。相对导入（`from . import x`）遵循 PEP 328 的
`resolve_name` 算法。

## API 参考

逐成员说明见 [PyEnvironment](../api/PyEnvironment.md)。
