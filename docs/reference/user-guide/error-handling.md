# 错误处理

源码：`PySharp/Runtime/PyRuntimeException.cs`、`PySharp/Modules/Builtins/PyExceptionObject.cs`。

Python 侧未捕获的异常不会以原始形式穿透到 .NET，而是以 `PyRuntimeException`（命名空间
`PySharp.Runtime`）的形式冒泡到 C# 调用方。静态 `RunFile`、`RunCode` 与实例 `Execute` 都遵循这一
行为：抛出前先经 `PyTryCatch` 处理，向环境错误流写出 traceback 并设置环境退出码。传播面的完整
说明见[执行 Python 代码](./executing-python.md)与 [PyInterpreter 参考](../api/PyInterpreter.md)。

## PyRuntimeException

```csharp
public class PyRuntimeException : Exception
{
    public PyRuntimeException(PyExceptionObject exception);
    public PyRuntimeException(PyCallContext context, PyExceptionObject exception);

    public override string Message { get; }            // Python 风格的 traceback 文本
    public PyExceptionObject PyException { get; }      // 原始 Python 异常对象
}
```

`Message` 是格式化后的 Python traceback（含源码行定位），可直接展示给用户。`PyException` 返回
`PyExceptionObject`，用于程序化检查异常类型与参数。

## PyExceptionObject 的公共成员

| 成员 | 说明 |
| --- | --- |
| `IReadOnlyList<PyObject> Args { get; }` | 异常构造参数，即 `raise ValueError("x", 1)` 的元组 |
| `PyExceptionObject? Cause { get; }` | `raise ... from ...` 设置的 `__cause__` |
| `PyExceptionObject? Context { get; }` | `__context__`，处理过程中隐式链上的原异常 |
| `bool SuppressContext { get; }` | `__suppress_context__` |
| `TracebackInfo? Traceback { get; }` | traceback 信息，内部维护 |

## 检查异常类型

每种 Python 异常类型对应一个 `Py*ErrorObjectType`（如 `PyTypeErrorObjectType`、
`PyValueErrorObjectType`、`PyIndexErrorObjectType`），共 68 个。通过 `<Type>.Shared` 取单例并用
`IsInstance` 判断：

```csharp
using PySharp.Modules.Builtins;
using PySharp.Runtime;

try
{
    PyInterpreter.RunCode("x = [1, 2]\nx[5]");
}
catch (PyRuntimeException e)
{
    if (PyIndexErrorObjectType.Shared.IsInstance(e.PyException))
        Console.WriteLine("越界了");
    else if (PyTypeErrorObjectType.Shared.IsInstance(e.PyException))
        Console.WriteLine("类型错误");
    else
        throw;   // 不认识的异常继续上抛
}
```

## 读取异常参数

`Args` 是 `PyObject` 列表，按 Python 侧传入的实际类型拆箱，值的公共访问器见
[在 C# 中操作 Python 对象](./python-objects-from-csharp.md)：

```csharp
catch (PyRuntimeException e) when (PyValueErrorObjectType.Shared.IsInstance(e.PyException))
{
    var arg = e.PyException.Args[0];
    if (arg is PyStrObject str)
        Console.WriteLine(str.Value);   // 异常消息文本
}
```

需要把执行结果数据带回 C# 时，可以在 Python 侧抛出携带数据的异常，在 C# 侧解析 `Args`。
当前版本读取模块全局变量尚无公共 API，见
[执行 Python 代码的限制说明](./executing-python.md#当前限制)。

## SystemExit 与退出码

`sys.exit()` 通过抛出 `SystemExit` 异常实现，它通常不是错误。`PyTryCatch` 会从异常的 `code`
属性解析出退出码并写入环境的 `ExitCode`（`internal`，由 CLI 等顶层路径消费），随后异常继续抛出
（REPL 情形除外，见下）。宿主若要自行决定进程状态，可拦截并转换：

```csharp
try
{
    PyInterpreter.RunFile("script.py");
    return 0;
}
catch (PyRuntimeException e)
{
    if (PySystemExitObjectType.Shared.IsInstance(e.PyException))
        return GetExitCode(e.PyException);

    Console.Error.WriteLine(e.Message);   // traceback 已写入环境错误流，此处由宿主决定如何呈现
    return 1;
}

static int GetExitCode(PyExceptionObject exception)
{
    if (exception.Args.Count is 0)
        return 0;

    return exception.Args[0] switch
    {
        PyIntObject i when i.IsInt32 => i.Int32Value,   // sys.exit(n) 得 n
        PyIntObject => 1,                               // 超出 int 范围的整数得 1
        _ => 1,                                         // sys.exit("msg") 等得 1
    };
}
```

这里给出的 `GetExitCode` 依据公共的 `Args` 判断，与库内部的 `ExitCode` 解析规则在
`code` 属性被改写的场景下可能不同（内部实现读取的是 `code` 属性，而非 `args[0]`）。需要完全一致的
行为时，应把退出码的处理留在 CLI 路径。

退出码的解析规则：异常的 `code` 属性为 `None` 或不存在时为 0；为整数时取其值并截断为 32 位，
超出 64 位有符号范围时为 -1；其他类型（如 `sys.exit("msg")`）会先打印该对象再以 1 退出。
REPL（`RunRepl`）循环内会自行处理异常并继续会话，`sys.exit` 同样被捕获，会话不因此结束。

## 异常组

Python 3.11 的异常组（`ExceptionGroup`、`BaseExceptionGroup`）已支持，`except*` 语法在语言侧
可用，同一组异常可被多个 `except*` 分支按类别并行捕获（语义为 PEP 654 的子集匹配，未匹配的剩余组
继续传播）。C# 侧的对应关系：

- 类型判定可用：传播出来的组异常是普通 `PyExceptionObject`，
  `PyExceptionGroupObjectType.Shared.IsInstance(...)` 与 `PyBaseExceptionGroupObjectType.Shared`
  为公共判定面。
- 子异常遍历当前没有稳定的公共 API：组结构（子异常列表与消息）的访问器为 `internal`。公共的 `Args`
  可以取到消息字符串与子异常，但扁平与嵌套形态随构造路径而异，不宜依赖。稳定做法是在 Python 侧处理，
  即用 `except*` 分支或 `e.split(...)` 拆包后再决定传给 C# 什么。
- `__cause__` 与 `__context__` 链同样可遍历。算法层细节见
  [异常组](../internals/exception-groups.md)。

## 异常链属性的 Python 侧语义

`__cause__`、`__context__` 与 `__suppress_context__` 对 Python 侧可写（上表是 C# 侧的读取视角）：

- `__cause__` 赋异常实例时，按 CPython 的 `PyException_SetCause` 语义同时把 `__suppress_context__`
  置为 `True`；赋 `None` 仅清空 cause。
- `__context__` 赋值不影响抑制标志。
- `__suppress_context__` 仅接受 bool。
- 删除三者抛精确的 `TypeError`；`__cause__` 与 `__context__` 赋非异常值时各报专属消息。

## 其他错误

- 编译期错误（语法错误）同样以 `PyRuntimeException` 抛出，`PyException` 为
  `PySyntaxErrorObjectType`（含 `PyIndentationErrorObjectType` 等子类）实例。
- 非源自 Python 逻辑的 .NET 错误（如 `RunFile` 的文件不存在）以原生 .NET 异常形式抛出，
  不会包装成 `PyRuntimeException`。
