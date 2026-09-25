# 执行 Python 代码

源码：`PySharp/Runtime/PyInterpreter.cs`。

PySharp 提供两层执行 API：

- 静态便捷方法：`PyInterpreter.RunFile`、`RunCode`、`RunRepl`，一行完成「建环境并执行」，
  适合脚本与工具场景。`RunCode` 与 `RunRepl` 另有接受现成 `PyEnvironment` 的重载。
- 受控解释器：`PyInterpreter.Create(environment)` 配合 `Execute`，由调用方构建并持有
  `PyEnvironment`，适合嵌入场景，可自定义 I/O 并复用会话状态。

## 静态便捷方法

### RunCode：执行源码字符串

```csharp
public static PyModuleObject? RunCode(
    string code,
    string? moduleName = null,   // 默认 "<module>"
    string? sourceName = null,   // 默认 "<string>"，出现在 traceback 中
    IEnumerable<string>? args = null)

public static PyModuleObject? RunCode(
    PyEnvironment environment,   // 在指定环境中执行，I/O 与文件系统由该环境决定
    string code,
    string? moduleName = null,
    string? sourceName = null)
```

无环境重载内部使用控制台宿主加内存文件系统。`sys.argv[0]` 固定为 `"-c"`，`args` 追加在其后。
返回执行后的模块对象。

### RunFile：运行脚本文件

```csharp
public static PyModuleObject RunFile(string filename, IEnumerable<string>? args = null)
```

从物理磁盘读取文件（`File.ReadAllBytes`），文件不存在时抛 `System.IO.FileNotFoundException`。
`sys.argv[0]` 为传入的 `filename`，脚本所在目录自动加入 `sys.path`，因此脚本可以 `import`
同目录模块。使用物理文件系统的控制台宿主。静态便捷方法中只有 `RunFile` 使用物理文件系统，
`RunCode` 与 `RunRepl` 挂载的是内存文件系统。

### RunRepl：交互式解释器

```csharp
public static void RunRepl(PyEnvironment? environment = null)
```

启动交互式 REPL，横幅为 `PySharp (v<版本>) on <操作系统>`，使用 `>>>` 与 `...` 提示符，支持多行
块输入（依据语法错误反馈决定是否续行）。可传入已有的 `PyEnvironment`，省略时以 REPL 宿主新建。
循环内发生的 Python 异常打印后继续会话，`sys.exit` 同样被捕获处理；输入流结束时（EOF）退出。
该方法持续读取输入，正常退出前不返回。

## 受控解释器

嵌入场景下推荐自己持有环境与解释器实例：

```csharp
using PySharp.Runtime;
using PySharp.Runtime.Environments;

using var environment = PyEnvironmentHost.CreateConsole().CreateEnvironmentBuilder()
    .AddPath("./scripts")
    .AddArg("-c")
    .Build();

using var interpreter = PyInterpreter.Create(environment);

interpreter.Execute("import helper\nhelper.run()", "<main>");
interpreter.Execute("print('done')", "<done>");
```

```csharp
public static PyInterpreter Create(PyEnvironment environment)

public void Execute(string code, string sourceName)   // 编译并执行一段源码
public void Execute(PyCodeObject codeObj)             // 执行已编译的代码对象
public PyModuleObject MakeModule(string moduleName)   // 基于当前 __main__ 状态派生新模块
public void Dispose()
```

行为要点：

- 同一 `PyInterpreter` 的多次 `Execute` 共享 `__main__` 模块：前一次执行定义的变量、函数与导入
  的模块，后一次执行都能看到。
- `Execute` 对 `code` 与 `sourceName` 做 `ArgumentNullException` 检查。执行中未捕获的 Python 异常
  先经 `PyTryCatch` 处理（向环境错误流写出 traceback，宿主支持颜色时带 ANSI 红，并设置环境退出码），
  随后以 `PyRuntimeException` 抛给调用方；`SystemExit` 的退出码同样先记入环境再抛出。
- `Execute(PyCodeObject)` 接受预编译的代码对象。`PyCodeObject` 暴露 `Bytecode`、`Name`、`Filename`、
  `ArgCount`、`VarNames`、`CellVars`、`FreeVars` 等只读属性；Python 侧可通过内建函数 `compile()`
  获得。
- `MakeModule` 把当前 `__main__` 的全部属性复制到新的 `PyModuleObject` 并返回，新模块的 `__name__`
  为传入名。适合「执行一段代码后把它封装成可 `import` 的模块」的模式。
- `Dispose` 释放内部执行上下文与环境。`PyInterpreter` 与 `PyEnvironment` 都应成对释放，
  推荐使用 `using`。

## 环境生命周期

`Execute` 依赖 `PyEnvironment` 提供的 I/O、路径与模块缓存等状态。环境与解释器的构建和配置见：

- [配置执行环境](./environment.md)：`sys.path`、`sys.argv` 与初始化选项
- [宿主与 I/O 重定向](./hosting.md)：stdout、stderr 与 stdin 的接管
- [虚拟文件系统](./virtual-file-system.md)：`open()` 与 `import` 背后的文件系统

## 当前限制

- 从 C# 侧读取模块全局变量暂无公共 API。`RunCode`、`RunFile` 与 `MakeModule` 返回的
  `PyModuleObject` 只公开 `Name` 与 `Origin`，模块属性字典属于内部实现。把数据传回 C# 的可行途径是：
  1. 重定向 stdout，让 Python 侧的 `print` 输出到调用方的流（`UseOut` 接入 `MemoryStream`），
     见[宿主与 I/O 重定向](./hosting.md)。
  2. 抛出携带数据的自定义异常，在 C# 侧从 `PyRuntimeException.PyException.Args` 读取，
     见[错误处理](./error-handling.md)。
- 动态调用 Python 对象（`Call`、`GetAttr` 等）需要 `PyCallContext`。该类型没有公共构造入口，
  仅在扩展点（自定义类型或模块方法的实现）中由运行时提供，见
  [用 C# 定义 Python 类型](./custom-types.md)。

## API 参考

逐成员说明见 [PyInterpreter](../api/PyInterpreter.md)。
