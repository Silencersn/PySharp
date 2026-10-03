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
| `SetEnvData(string, object?)` | 按键注入环境数据，同键覆盖，见[环境数据注入](#环境数据注入) |
| `bool TryGetEnvData(string, out object?)` | 按键读取环境数据，键未设置返回 `false` |
| `bool TryGetEnvData<T>(string, out T?)` | 泛型读取，键未设置或存储值不能转换为 `T` 时返回 `false` |
| `bool RemoveEnvData(string, out object?)` | 按键移除并取回，键未设置返回 `false` |
| `event Action? OnDisposing` | `Dispose` 开始时触发一次的释放前钩子，见[释放与退出流程](#释放与退出流程) |
| `static CreateNull()` | 空宿主（`Stream.Null` 三流加空内存文件系统）的最小环境 |
| `static CreateConsole()` | 控制台宿主的便捷环境，等价于 `PyEnvironmentHost.CreateConsole().CreateEnvironmentBuilder().Build()` |
| `Dispose()` | 执行退出流程并释放资源 |

## 环境数据注入

宿主可以把任意 C# 数据挂到环境上，供扩展实现（自定义模块与类型的方法体）读取。典型用途是把
每次运行各不相同的数据（如一份快照）交给扩展函数，替代静态字段或闭包模块这类绕行方案：

```csharp
using var environment = host.CreateEnvironmentBuilder()
    .AddModuleProvider(provider)
    .Build();

environment.SetEnvData("snapshot", snapshot);    // 构建后、执行前注入

using var interpreter = PyInterpreter.Create(environment);
interpreter.Execute(code, "<console>");
```

扩展实现经 `PyCallContext.PyEnvironment`（公共属性）读回环境，再取注入数据：

```csharp
[PyFunctionParameters()]
private static PyResult CountImpl(PyCallContext context, PyArguments arguments)
{
    if (!context.PyEnvironment.TryGetEnvData<Snapshot>("snapshot", out var snapshot))
        return PyResult.TypeError("snapshot was not injected");

    // … 用 snapshot 构造返回值 …
}
```

`OnImport(PyCallContext, PyEnvironment)` 与模块提供器本来就拿到环境，可直接
`TryGetEnvData`。三条契约：

- **生命周期**：数据随环境实例消亡；`Dispose` 不会调用注入值的 `Dispose`——宿主保有所有权，
  环境无从判断某个值是否仍被共享。需要提前回收单个键时用 `RemoveEnvData`；需要在环境销毁时
  整批回收（如包装了流或非托管资源）时，订阅 `OnDisposing`。
- **并发**：底层存储是 `ConcurrentDictionary`，任意时刻的读写与撤销都并发安全；典型用法仍是
  构建后、执行前注入，执行期间只读。
- **沙箱边界**：注入值以原生 C# 对象存放于环境，库不为其创建 Python 包装、不注册任何 Python
  可见名字，Python 代码（含 `import` 与内省）无法触达。注入通道绝不能用来把宿主对象暴露给
  Python——那需要走[用 C# 定义 Python 类型](./custom-types.md)的受支持路线。

## 释放与退出流程

`Dispose` 开始时先触发一次 `OnDisposing` 事件，随后中断（`Interrupt`）并等待（`Join`）环境中
登记的所有 Python 线程，即 `threading` 模块创建的线程，最后释放三条标准流；订阅者抛出的异常
不会阻断这条清理链，但会在清理完成后从 `Dispose` 抛出。环境数据随之废弃（由 GC 回收），注入值
本身的释放由宿主负责——`OnDisposing` 就是宿主做这件事的钩子。`sys.exit` 的退出码由解释器记录在
环境的 `ExitCode`（`internal`），由顶层运行路径与 CLI 决定进程行为，环境退出不会直接结束进程。

## sys.path 与 import

搜索路径按以下顺序解析模块，均未命中时抛 `ModuleNotFoundError`：

1. 内建模块：`PyStandardLibrary` 注册表中的模块（`builtins`、`sys`、`math` 等）。
2. 路径模块：依次扫描 `sys.path` 中的目录，寻找 `<name>.py` 文件或含 `__init__.py` 的 `<name>/`
   包目录，内容读取自环境的虚拟文件系统。

`sys.path` 是 Python 侧的活动列表：脚本里的 `sys.path.append` / `insert` / `remove` 对下一次
import 立即生效，构建器 `AddPath` 预置的目录就是它的初始内容。`sys.modules` 同样是 Python 侧的
权威导入注册表，删除条目强制重载、赋值条目在 import 时原样返回。搜索路径指向的目录由宿主的
文件系统解释（内存 FS 或物理 FS），见[虚拟文件系统](./virtual-file-system.md)。相对导入
（`from . import x`）遵循 PEP 328 的 `resolve_name` 算法。

## API 参考

逐成员说明见 [PyEnvironment](../api/PyEnvironment.md)。
