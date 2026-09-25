# 宿主与 I/O 重定向

源码：`PySharp/Runtime/Environments/PyEnvironmentHost.cs`、`PyEnvironmentHostBuilder.cs`、
`PyEnvironment.cs`。

`PyEnvironmentHost` 决定 Python 代码看到的外部世界：标准输入、标准输出、标准错误与文件系统。
三条标准流以 `Stream` 抽象提供。环境构造时从宿主各分配一次流，按编码包装成内部读写器后持有到底，
环境创建后不能更换宿主。

## 预定义宿主

| 工厂 | stdin | stdout 与 stderr | 文件系统 |
| --- | --- | --- | --- |
| `CreateNull()` | `Stream.Null` | `Stream.Null` | 空内存 FS |
| `CreateConsole()` | `Console.OpenStandardInput()` | `Console.OpenStandardOutput()`、`OpenStandardError()` | 空内存 FS |
| `CreateConsole(usingPhysicalFileSystem: true)` | 同上 | 同上 | 物理文件系统 |
| `CreateRepl()` | 同上 | 同上 | 空内存 FS |
| `CreateBuilder()` | 可配置 | 可配置 | 可配置 |

两点需要注意：

- 默认的控制台宿主挂载内存文件系统。`import` 任何 `.py` 文件或 `open()` 磁盘文件都需要
  `usingPhysicalFileSystem: true` 或自定义文件系统；`PyInterpreter.RunFile` 内部使用的就是物理
  文件系统版本的宿主。见[虚拟文件系统](./virtual-file-system.md)。
- 编码：流按编码包装成读写器，默认取宿主的 `DefaultEncoding`（UTF-8 无 BOM）。控制台系宿主覆写了
  `CreateEnvironmentBuilder()`，把三条流编码预置为 `Console.InputEncoding` 与 `Console.OutputEncoding`，
  可用环境构建器的 `UseStd*Encoding` 覆盖。stdout 与 stderr 的包装器 `AutoFlush = true`，`print`
  的输出即时可见。

控制台系宿主还有两个虚属性 `SupportsColorOutput` 与 `SupportsErrorColorOutput`，默认值取终端状态与
环境变量，解释器据此决定向标准输出或错误输出写 traceback 时是否包 ANSI 颜色。宿主把输出接到不支持
ANSI 的地方（文件、管道、GUI 日志面板）时应覆写为 `false`，或经环境构建器的
`UseStdOutColorSupport` 与 `UseStdErrColorSupport` 关闭。

## 编码与标准流对象

编码选项只影响标准流，不影响 `open()` 打开的文件的默认行为：

- 影响链：环境构建器的 `UseStdInEncoding`、`UseStdOutEncoding`、`UseStdErrEncoding`，或一次性
  `UseStdioEncoding`，在环境构造时以该编码把三条 `Stream` 包成读写器，`print`、`input`、
  `sys.stdout` 等的文本进出都走它。未设置时用宿主的 `DefaultEncoding`。
- 文件文本编码独立：`open()` 有独立的 `encoding` 参数，默认 UTF-8，与流的编码选项无关，见
  [文件对象](./file-objects.md)。
- `sys.stdin`、`sys.stdout`、`sys.stderr` 以 `PyStdIoObject` 暴露给 Python：方法 `read([size])`、
  `readline([size])`、`write(data)`、`flush()`、`close()`、`readable()`、`writable()`，属性
  `closed`、`name`（值为 `"<stdin>"` 等）。`input()` 读取 stdin，EOF 时抛 `EOFError`；prompt 写
  stdout。`sys.stdout.write()` 与 `print` 落到同一条流。

宿主侧示例，让 Python 侧以 GBK 读写控制台：

```csharp
using var environment = PyEnvironmentHost.CreateConsole()
    .CreateEnvironmentBuilder()
    .UseStdioEncoding(Encoding.GetEncoding("GBK"))
    .AddArg("-c")
    .Build();
```

## 自定义宿主

`CreateBuilder()` 返回公共接口 `IPyEnvironmentHostBuilder`：

```csharp
public interface IPyEnvironmentHostBuilder
{
    IPyEnvironmentHostBuilder UseIn(Stream reader);
    IPyEnvironmentHostBuilder UseOut(Stream writer);
    IPyEnvironmentHostBuilder UseError(Stream writer);
    IPyEnvironmentHostBuilder UseFileSystem(IVirtualFileSystem fileSystem);
    PyEnvironmentHost Build();
}
```

未配置的项使用默认值（`Stream.Null` 与空内存 FS）。构建出宿主后，经 `host.CreateEnvironmentBuilder()`
进入环境构建器，见[配置执行环境](./environment.md)。

## 典型用法

### 捕获 Python 输出

把 `print` 的输出收进 `MemoryStream` 是把执行结果传回 C# 的常用方式：

```csharp
using System.Text;
using PySharp.Runtime;
using PySharp.Runtime.Environments;

var output = new MemoryStream();
var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(output)
    .UseError(Stream.Null)            // 也可另接一个 MemoryStream 分流错误输出
    .Build();

using var environment = host.CreateEnvironmentBuilder().AddArg("-c").Build();
using var interpreter = PyInterpreter.Create(environment);

interpreter.Execute("print(sum(range(101)))", "<calc>");

var text = Encoding.UTF8.GetString(output.ToArray()).Trim();
Console.WriteLine(text);   // 5050；stdout 包装器 AutoFlush，Execute 返回即可读
```

编码默认为宿主的 `DefaultEncoding`，需要其他编码时用环境构建器的 `UseStdOutEncoding`。

### 喂入脚本输入

```csharp
var input = new MemoryStream(Encoding.UTF8.GetBytes("Alice\n30\n"));
var host = PyEnvironmentHost.CreateBuilder()
    .UseIn(input)
    .UseOut(Console.OpenStandardOutput())
    .Build();

using var environment = host.CreateEnvironmentBuilder().AddArg("-c").Build();
using var interpreter = PyInterpreter.Create(environment);

interpreter.Execute("name = input()\nage = int(input())\nprint(f'{name} is {age}')", "<form>");
// 输出：Alice is 30
```

### 沉默模式

测试或批量执行时不希望任何输出到达控制台：不调用 `UseIn`、`UseOut`、`UseError` 即为 `Stream.Null`，
或直接使用 `CreateNull()` 宿主。

## 宿主抽象成员

自定义宿主可继承 `PyEnvironmentHost` 并覆写：

```csharp
public abstract class PyEnvironmentHost
{
    public virtual Encoding DefaultEncoding => Utf8NoBom;    // UTF-8 无 BOM
    public virtual bool SupportsColorOutput => true;
    public virtual bool SupportsErrorColorOutput => SupportsColorOutput;
    public virtual IPyEnvironmentBuilder CreateEnvironmentBuilder();  // 绑定本宿主的环境构建器
    public abstract Stream AllocateStdIn();
    public abstract Stream AllocateStdOut();
    public abstract Stream AllocateStdErr();
    public abstract IVirtualFileSystem FileSystem { get; }
}
```

三个 `Allocate*` 方法在环境构造时各调用一次，`FileSystem` 属性在需要访问文件时读取。多数场景用
构建器组合现成实现即可，无需继承。

## API 参考

逐成员说明见 [PyEnvironmentHost](../api/PyEnvironmentHost.md)。
