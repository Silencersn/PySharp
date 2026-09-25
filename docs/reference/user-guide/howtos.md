# 嵌入场景手册

本篇按任务组织，每个场景给出可直接改写的最小示例，全部使用当前公共 API。概念与逐成员参考见各
链接篇目。

## 场景 1：取回脚本的执行结果

适用规则计算、表达式求值、批处理。当前版本读取模块全局变量无公共 API，stdout 重定向是首选通道：

```csharp
using PySharp.Runtime;
using PySharp.Runtime.Environments;

var output = new MemoryStream();
var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(output)
    .UseError(Stream.Null)          // 沉默化错误流，也可分流到单独的 MemoryStream
    .Build();

using var environment = host.CreateEnvironmentBuilder().AddArg("-c").Build();
using var interpreter = PyInterpreter.Create(environment);

interpreter.Execute("print(sum(range(101)))", "<calc>");

var text = Encoding.UTF8.GetString(output.ToArray()).Trim();   // "5050"
```

让脚本侧约定输出格式（单值或 `key=value` 行），C# 侧解析。一次 `Execute` 配一个 `MemoryStream`，
避免多次执行互相污染。

## 场景 2：让脚本把结构化数据抛回来

适用需要多个返回值，或希望「取结果」与「报错」共用一条通道的场景。让 Python 侧抛出携带数据的异常，
C# 侧按类型拦截并读 `Args`：

```csharp
// Python 约定：完成时 raise ValueError("__done__", ...)
// 参数为 文本、数值、列表
const string script = """
    raise ValueError("__done__", "hello", 42, [1, 2, 3])
    """;

try
{
    PyInterpreter.RunCode(script, "<job>");
}
catch (PyRuntimeException e)
    when (PyValueErrorObjectType.Shared.IsInstance(e.PyException)
          && e.PyException.Args.Count is 4)
{
    var tag  = (PyStrObject)e.PyException.Args[0];   // "__done__"
    var text = ((PyStrObject)e.PyException.Args[1]).Value;
    var num  = ((PyIntObject)e.PyException.Args[2]).Int32Value;
    var list = (PyListObject)e.PyException.Args[3];
    // 类型读取规则见「类型映射与转换」
}
```

用第一个参数作标签区分正常完成与真实错误，未命中 `when` 过滤的异常继续上抛。详见
[错误处理](./error-handling.md#读取异常参数)。

## 场景 3：沙箱化执行不可信脚本

适用插件与用户脚本。PySharp 的 IO 边界很干净，`open()` 与 `import` 只经环境的虚拟文件系统，
没有其他磁盘通道。最小沙箱三件套：

```csharp
var fs = MemoryFileSystem.CreateBuilder()
    .WithWorkingDirectory("/sandbox")
    .WithFile("/sandbox/allowed_helper.py", "def base(n):\n    return n * 2")
    .Build();                                    // 白名单：只提供想给脚本的文件

var host = PyEnvironmentHost.CreateBuilder()
    .UseIn(Stream.Null)                          // 不给输入
    .UseOut(Stream.Null)                         // 或 MemoryStream 审计输出
    .UseFileSystem(fs)                           // 不挂 PhysicalFileSystem，脚本无法触盘
    .Build();

using var environment = host.CreateEnvironmentBuilder()
    .AddArg("-c")
    .AddPath("/sandbox")
    .Build();
```

边界认知：文件系统之外，脚本还能用的能力是内建函数与 13 个内嵌模块（没有 `os` 与 `subprocess`），
见[标准库模块覆盖](../python-compat/stdlib-modules.md)。CPU 时间与内存不受解释器限制，需宿主自行
约束，例如以超时包装执行线程。另一个约束手段是警告升级：让脚本执行
`warnings.simplefilter("error")` 后，`warnings.warn` 会在 Python 侧抛出警告异常，脚本内可自行
`try` 与 `except` 处理；宿主要在 C# 侧拦截则必须用 `RunCode` 或 `RunFile` 执行，见
[警告与数据类](./warnings-and-dataclasses.md)。

## 场景 4：把脚本组织成可 import 的模块

适用宿主提供一组 Python 编写的功能模块、主脚本按名取用的场景。内存文件系统是提供自定义 Python
模块的一等方式：

```csharp
var fs = MemoryFileSystem.CreateBuilder()
    .WithFile("/plugins/greeting.py", """
        def hello(name):
            return f'hello, {name}'
        """)
    .WithFile("/plugins/__init__.py", "")        // 也可组织为包
    .Build();

var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(Console.OpenStandardOutput())
    .UseFileSystem(fs)
    .Build();

using var environment = host.CreateEnvironmentBuilder()
    .AddArg("-c")
    .AddPath("/plugins")
    .Build();

using var interpreter = PyInterpreter.Create(environment);
interpreter.Execute("from greeting import hello\nprint(hello('PySharp'))", "<main>");
// hello, PySharp
```

模块源码可来自数据库或配置系统，写进 `MemoryFileSystem`（`WriteAllText`）即可。
`PyInterpreter.MakeModule(name)` 能把已执行代码的 `__main__` 状态复制成新的 `PyModuleObject`。
需要在 C# 里实现模块逻辑时走声明式扩展，见场景 6。

## 场景 5：多会话隔离与生命周期管理

适用每用户或每任务一个独立解释器实例的场景。`PyEnvironment` 之间状态完全隔离，I/O、`sys.path`
与模块缓存互不可见，各自 `Dispose`：

```csharp
PyInterpreter CreateSession(Stream output, string scriptDir)
{
    var host = PyEnvironmentHost.CreateBuilder()
        .UseOut(output)
        .UseFileSystem(PhysicalFileSystem.Shared)   // 需要读磁盘脚本时
        .Build();

    var environment = host.CreateEnvironmentBuilder()
        .AddArg("-c")
        .AddPath(scriptDir)
        .Build();                                    // AddPath 可多次调用

    return PyInterpreter.Create(environment);        // interpreter Dispose 会连带释放环境
}
```

`PyInterpreter` 与 `PyEnvironment` 应成对 `using`。同一会话内多次 `Execute` 共享 `__main__`，
前一段定义的函数对后一段可见，这是「加载脚本后多次调用其函数」的会话复用模式的基础。完整生命周期
语义见[执行 Python 代码](./executing-python.md)与[配置执行环境](./environment.md)。

多会话并行时环境之间没有共享状态：模块缓存、`sys.path`、字符串驻留池（`InternPool`）、警告过滤器
与去重状态都是每环境一份。唯一例外是整数字符串转换位数限制，它由 `sys.set_int_max_str_digits`
设置，是进程级全局，一个环境的调整对全部环境生效，见
[标准库覆盖](../python-compat/stdlib-modules.md)。.NET 侧共享的对象（自己创建的 `Py*Object`、
`MemoryFileSystem` 实例等）的线程安全仍由调用方保证，dict 等容器非线程安全，见
[常见问题](./faq.md)。驻留池隔离的含义是：同一句字面量在两个环境里是两个不同的 `PyStrObject`
实例，`PyId` 不同、`is` 判 `False`，但值比较 `==` 仍然成立，因此跨环境传字符串数据无需特殊处理。

## 场景 6：给脚本提供宿主能力

适用把 C# 库的能力暴露成 Python 类型、模块或函数的场景。不包装现有对象，而是用 attribute 声明新
类型，源生成器在编译期产出注册代码：

```csharp
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

[PyType("Widget")]
public sealed partial class WidgetObjectType : PyTypeObject<PyWidgetObject>
{
    [PyMethod("resize")]
    [PyFunctionParameters("width", "height", "/")]
    private static PyResult Resize(PyCallContext context, PyWidgetObject self, PyArguments arguments)
    {
        self.Width = ((PyIntObject)arguments[0]).Int32Value;
        self.Height = ((PyIntObject)arguments[1]).Int32Value;
        return PyNoneObject.None;
    }
}

public sealed partial class PyWidgetObject : PyObject   // 配对的值类型，承载字段
{
    public int Width { get; set; }
    public int Height { get; set; }
    public override PyTypeObject DefaultPyType => WidgetObjectType.Shared;
}
```

Python 侧即可调用 `w.resize(640, 480)`。参数读取按[类型映射与转换](./type-mapping.md)的规则；
完整模式（属性、构造、协议覆写、异常类型、模块）见
[扩展 PySharp](./extending-pysharp.md)与[用 C# 定义 Python 类型](./custom-types.md)、
[用 C# 编写 Python 模块](./custom-modules.md)。

扩展实现里还能发警告：`context.Warn(...)` 是 `PyCallContext` 上的 public 方法，重载含泛型版
`Warn<TWarning>(message)` 与 `WarnExplicit`，Python 侧的 `filterwarnings` 与 `catch_warnings`
过滤器对它同样生效。注意错误即值的读法：`"error"` 过滤器激活时，`Warn` 返回的是 `IsError` 的结果
而非成功值，因此扩展实现应检查返回值来决定是让策略经返回值冒泡，还是自行处置。

## 场景 7：交互式嵌入

最快路径是 `PyInterpreter.RunRepl()`，适合把 PySharp 当独立解释器用，循环内异常打印后继续。
需要自定义横幅或输入源时，用 `PyEnvironmentHost.CreateRepl()` 或构建器配
`Console.OpenStandardInput()` 自行组装，再循环喂 `Execute`，多行块可依据语法错误的反馈决定续行。
REPL 类宿主的文件系统默认是内存 FS，要让用户 `import` 本地模块需显式换物理 FS。

## 相关节点

[常见问题](./faq.md) · [执行 Python 代码](./executing-python.md) ·
[宿主与 I/O 重定向](./hosting.md) · [虚拟文件系统](./virtual-file-system.md) ·
[错误处理](./error-handling.md)
