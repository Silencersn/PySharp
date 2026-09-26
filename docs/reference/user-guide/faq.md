# 常见问题与故障排查

本篇按问题组织答案，每条答案的事实细节在链接的专题篇中展开。

## 执行与环境

问：`RunCode` 或 `RunFile` 返回了模块对象，怎么把脚本里算出的变量取回 C#？

当前版本读取模块全局变量暂无公共 API，`PyModuleObject` 只公开 `Name` 与 `Origin`。两条可行途径：
重定向 stdout，让 Python 侧 `print` 到调用方的流（`MemoryStream`，经宿主 `UseOut` 接入），见
[宿主与 I/O 重定向](./hosting.md#捕获-python-输出)；或让脚本抛出携带数据的自定义异常，在 C# 侧从
`PyRuntimeException.PyException.Args` 解析，见[错误处理](./error-handling.md#读取异常参数)。限制
原文见[执行 Python 代码](./executing-python.md#当前限制)。

问：为什么脚本 `import` 不到磁盘上的 `.py` 文件？

因为宿主默认挂载的是内存文件系统，`import` 与 `open()` 只经由环境的 `IVirtualFileSystem`，见
[虚拟文件系统](./virtual-file-system.md)。三个解法：用 `PyInterpreter.RunFile`，它内部使用物理 FS
宿主且脚本目录自动进 `sys.path`；用 `CreateConsole(usingPhysicalFileSystem: true)`；或经构建器
`UseFileSystem(PhysicalFileSystem.Shared)`。

问：想给脚本提供自己写的 Python 模块，怎么做？

推荐内存文件系统：`MemoryFileSystem.CreateBuilder().WithFile("/lib/m.py", 源码)` 加
`AddPath("/lib")`，模块即可被 `import`。这是嵌入者提供自定义 Python 模块的一等方式，完整示例见
[虚拟文件系统](./virtual-file-system.md#与-import-和-open-的关系)。C# 侧实现的模块可经
`AddModuleProvider` 挂进环境提供器链，方式见
[用 C# 编写 Python 模块](./custom-modules.md#模块解析与-pymoduleprovider)。

问：`sys.exit()` 会发生什么？

`sys.exit` 通过抛出 `SystemExit` 异常实现，通常不是错误。`RunFile`、`RunCode` 与 `Execute` 都会
把它抛给调用方，宿主应捕获 `PyRuntimeException` 并用 `PySystemExitObjectType.Shared.IsInstance`
判断，再转换为退出码，代码模板见[错误处理](./error-handling.md#systemexit-与退出码)。`RunRepl`
例外，它在循环内自行消化异常并继续会话，退出码记入环境。

问：多次 `Execute` 之间状态共享吗？多个 `PyEnvironment` 能共存吗？

同一 `PyInterpreter` 的多次 `Execute` 共享同一个 `__main__`，前一次定义的函数与导入的模块对后一次
可见。多个 `PyEnvironment` 实例可以共存，各自持有独立的 I/O、路径与模块缓存。每个都应 `Dispose`，
推荐用 `using`。见[执行 Python 代码](./executing-python.md)与
[配置执行环境](./environment.md)。

问：在多个 .NET 线程里跑 Python 代码要注意什么？

解释器没有 GIL。跨线程共享的可变对象依赖其自身线程安全，内建容器中 dict 是非线程安全的实现，
跨线程共享需外部加锁，见 [threading 与 queue](../internals/threading-and-queue.md)。Python 侧的
`threading.Thread` 可用，环境退出时会中断并等待收尾。

## 数据交换与互操作

问：能把 C# 的 `int` 或 `string` 直接当参数传给 Python，或在扩展方法签名里使用吗？

不能。PySharp 没有动态互操作层，C# 与 Python 的值交换全部走强类型 `Py*Object` API：构造方向用静态
工厂（`PyIntObject.FromInteger(42)`），读取方向用公共属性（`str.Value`）。扩展方法的形参与返回值
同样必须是 `PyObject` 族类型，没有 .NET 原生类型的自动转换。完整映射表见
[类型映射与转换](./type-mapping.md)。

问：扩展方法里 `return exc;` 返回了异常对象，为什么 Python 侧没抛异常？

`PyExceptionObject` 也是 `PyObject`，直接返回走的是 `PyObject` 到成功结果的隐式转换，异常被当成了
普通返回值。必须包一层，即 `return new PyExceptionResult(exc);` 或
`return PyResult.FromException(exc);`。这是 `PyResult` 错误即值模型最典型的陷阱，见
[运算符与协议分发](./operators-and-protocols.md)。

问：Python 的 `int` 在 C# 侧是什么类型？

`PyIntObject.Value` 是 `BigInteger`，因为 Python 整数是任意精度的。确认在 `int` 范围内后用
`IsInt32` 检查并读取 `Int32Value`；越界时取 `Int32Value` 会抛以 `OverflowError` 为内容的
`PyRuntimeException`。

## 扩展开发

问：`PyCallContext` 从哪里来？为什么构造不出来？

该类型没有公共构造入口，这是刻意的 API 边界。上下文只在扩展点内由运行时提供，即
`[PyMethod]` 或 `[PyExport]` 标注方法的 `context` 参数，或 `PyModuleObject.OnImport` 的参数。
在扩展实现内部可以自由使用全部协议 API（`Call`、`GetAttr`、`PyOperators` 等）；扩展之外的纯宿主
代码只能用无上下文 API。见
[在 C# 中操作 Python 对象](./python-objects-from-csharp.md#协议操作与-pycallcontext)。

问：自定义类型可以手写 `Shared` 单例、`FillSlots` 或 `RegisterMethods` 吗？

不可以。这些样板由源生成器按 `[PyType]` 等特性自动产出，手写会与生成代码重复定义而冲突。声明
`partial` 类并标注特性即可，见[用 C# 定义 Python 类型](./custom-types.md)。

问：`PyFunctionParameters` 签名串写错会怎样？

签名不匹配在调用时以 Python 风格的 `TypeError`（缺参、多参或未知关键字）呈现，由
`PyArgsValidator` 按 `[PyFunctionParameters]` 声明校验。签名串的写法（`"/"`、`"key=default"`、
`"*args"`、`"**kwargs"`）见[扩展 PySharp](./extending-pysharp.md)。

问：扩展实现里怎么发警告？

`context.Warn(message)`、`context.WarnExplicit(...)` 是 `PyCallContext` 上的 public 方法，
在 `[PyMethod]` 与 `[PyExport]` 实现内直接可用。Python 侧的 `filterwarnings` 与
`catch_warnings` 过滤器对其生效。详见[警告与数据类](./warnings-and-dataclasses.md)。

## 兼容性

问：脚本用了 `os`、`re`、`json`，能跑吗？

内嵌标准库当前是 13 个模块的高频子集，包括 `builtins`、`sys`、`site`、`operator`、`math`、
`time`、`random`、`this`、`dataclasses`、`threading`、`queue`、`typing`、`warnings`。`os`、`re`、
`json`、`collections`、`itertools`、`functools`、`pathlib` 尚不可用。先查
[标准库模块覆盖](../python-compat/stdlib-modules.md)，再对照
[语言特性支持清单](../python-compat/language-features.md)与
[与 CPython 的差异](../python-compat/cpython-differences.md)评估。

问：`sys.version`、`sys.flags`、`sys.modules`、`sys.path` 能用吗？

当前不可用。这些成员尚未暴露到 Python 侧，`sys.path` 与 `sys.modules` 在运行时内部维护，import
解析不经由它们。脚本里用到即报 `AttributeError`。需要版本信息时可从 C# 侧取程序集版本传入。
`sys` 已有的成员是 `argv`、`stdin`、`stdout`、`stderr`，以及
`get_int_max_str_digits` 与 `set_int_max_str_digits`，见
[标准库覆盖](../python-compat/stdlib-modules.md)。缺口跟踪见
[路线图](../contributing/roadmap.md)。

问：有 `.pyc` 字节码缓存吗？

没有。不产生也不读取 `.pyc`，每次 `import` 都重新编译。这是当前实现的有意取舍，见
[与 CPython 的差异](../python-compat/cpython-differences.md)。

问：错误消息和 CPython 完全一样吗？

异常的类型与层级一致。消息措辞以 CPython 为基准持续对齐，资源串集中在 `PySR`，已经完成运算符
报文、str 方法族 `TypeError`、容器构造参数校验与异常链属性删除报文等大批量对齐，但独立实现仍可能
有零星出入。逐字对照 CPython 输出的测试不保证全部通过。

## 故障排查

按现象分层：

1. 语法与编译错误：以 `PyRuntimeException` 抛出，`PyException` 为 `PySyntaxErrorObjectType`
   （含 `IndentationError` 子类）。`Message` 是含源码行定位的 traceback，检查传入
   `Execute(code, sourceName)` 的 `sourceName` 是否可辨识，它出现在 traceback 中。
2. 运行时错误：直接读 `e.Message`，它是 Python 风格的 traceback，可原样展示。按类型分支处理见
   [错误处理](./error-handling.md#检查异常类型)。
3. import 与文件问题：报 `ModuleNotFoundError` 时先查两件事，即 `sys.path` 是否有目标目录
   （`AddPath`），以及宿主挂载的文件系统里是否真有那个文件（默认是内存 FS）。
4. C# 侧异常：`PyDictObject` 的 `this[string]` 读取未命中抛 .NET 的 `KeyNotFoundException`，
   不是 Python 的 `KeyError`，应先用 `TryGetValue` 预判；`RunFile` 的文件不存在抛
   `System.IO.FileNotFoundException`。
5. 扩展行为异常（方法没注册、参数对不上、生成代码可疑）：转入贡献者调试流程。测试工程可访问
   `internal` 成员（`InternalsVisibleTo`），读生成器产物的方式见
   [调试指南](../contributing/debugging.md)与
   [源生成器与代码分析](../internals/source-generators.md)。

## 相关节点

[执行 Python 代码](./executing-python.md) · [错误处理](./error-handling.md) ·
[类型映射与转换](./type-mapping.md) · [嵌入场景手册](./howtos.md) ·
[与 CPython 的差异](../python-compat/cpython-differences.md)
