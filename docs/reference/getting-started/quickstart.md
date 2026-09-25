# 快速上手

本篇用最短的路径把 PySharp 跑起来。示例只使用核心库的公共 API。

## 执行代码字符串

```csharp
using PySharp.Runtime;

PyInterpreter.RunCode("print('hello from PySharp')");
```

`RunCode` 接受一段 Python 源码并执行，可选参数如下：

```csharp
// 完整签名
PyInterpreter.RunCode(
    code,        // 必填：Python 源码
    moduleName,  // 可选：模块名，默认 "<module>"
    sourceName,  // 可选：源码显示名（出现在 traceback 中），默认 "<string>"
    args);       // 可选：IEnumerable<string>?，透传到 sys.argv（sys.argv[0] 为 "-c"）
```

## 运行脚本文件

```csharp
PyInterpreter.RunFile("script.py", ["arg1", "arg2"]);
```

`RunFile` 从磁盘读取 `.py` 文件执行。`sys.argv[0]` 为脚本路径，后续元素来自第二个参数；
脚本所在目录会被加入模块搜索路径（`sys.path`）。

## 交互式 REPL

```csharp
PyInterpreter.RunRepl();
```

进入交互式解释器（`>>>` 与 `...` 提示符），行为类似 CPython 的 REPL：会话持续读取输入直到进程结束，
该方法不返回。

## 处理 Python 异常

静态便捷方法在脚本抛出未捕获的 Python 异常时，以 `PyRuntimeException` 的形式抛到 C#：

```csharp
using PySharp.Runtime;

try
{
    PyInterpreter.RunCode("raise TypeError('boom')");
}
catch (PyRuntimeException e)
{
    Console.WriteLine(e.Message);      // Python 风格的 traceback 文本
    var pyExc = e.PyException;         // PyExceptionObject，可检查异常类型与 args
}
```

实例方法 `Execute` 的行为不同，它打印异常后不再抛出。详见[错误处理](../user-guide/error-handling.md)。

## 使用受控环境

便捷方法每次都会新建一套控制台环境。需要接管 I/O 或在同一环境上多次执行时，用构建器组装：

```csharp
using System.Text;
using PySharp.Runtime;
using PySharp.Runtime.Environments;

// 1. 宿主：决定标准输入输出与文件系统
var output = new MemoryStream();
var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(output)                    // 把 Python 的 stdout 接到内存流
    .Build();

// 2. 环境：搜索路径、程序参数、初始化选项
using var environment = host.CreateEnvironmentBuilder()
    .AddArg("-c")
    .Build();

// 3. 解释器：在同一环境上反复执行
using var interpreter = PyInterpreter.Create(environment);
interpreter.Execute("print('first')", "<embed1>");
interpreter.Execute("print('second')", "<embed2>");

Console.WriteLine(Encoding.UTF8.GetString(output.ToArray()));  // first\nsecond
```

三个对象的分工：

- `PyEnvironmentHost`：标准 I/O（`Stream` 抽象，按宿主编码包装）与虚拟文件系统的来源。
- `PyEnvironment`：一次 Python 会话的状态，包括模块缓存、字符串驻留池、搜索路径与 `sys.argv`。
- `PyInterpreter`：在环境上执行代码的入口，持有 `__main__` 模块。

`PyEnvironment` 与 `PyInterpreter` 都实现 `IDisposable`，推荐用 `using` 释放。释放环境会中断并等待
其登记的 Python 线程。I/O 接管的完整机制（流编码、颜色开关）见
[宿主与 I/O 重定向](../user-guide/hosting.md)。

## 下一步

- [执行 Python 代码](../user-guide/executing-python.md)：两层执行模型的完整说明
- [宿主与 I/O 重定向](../user-guide/hosting.md)：stdin、stdout、stderr 的接管
- [在 C# 中操作 Python 对象](../user-guide/python-objects-from-csharp.md)：值的构造与读取
