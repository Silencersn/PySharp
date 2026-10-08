# 与 CPython 的差异

PySharp 以 CPython 3 为行为参照，大量语义细节（反射协议、子类优先、协议回退、错误类型、格式化边界）
按 CPython 对齐并有回归测试覆盖，但不承诺 100% 兼容。本篇列出当前已知、对脚本作者与嵌入者可见的
差异和限制。清单不追求穷尽，具体行为以实现为准。

## 标准库与内建函数

- 标准库覆盖面有限：内嵌模块共 13 个，见[标准库模块覆盖](./stdlib-modules.md)。CPython 的大量
  模块（`os`、`re`、`json`、`collections`、`itertools`、`functools`、`pathlib` 等）尚不可用。
- `sys` 的常用面（`path`、`modules`、`exit`、`version_info`、`flags`、`implementation`、
  `excepthook` 等）已可用，成员清单见[标准库模块覆盖](./stdlib-modules.md)。仍存的差异：
  `sys.implementation.name` 报 `"cpython"`（与 `version_info` 自洽）；`sys.prefix` 取宿主
  `AppContext.BaseDirectory`；`flags.utf8_mode` 恒为 0（不读 `PYTHONUTF8`），
  `dont_write_bytecode` 恒为 1；`intern` 仅收精确 `str` 的语义与 CPython 一致，但驻留池为
  每环境一份。
- structseq 类型的模块归属：`sys.version_info` 与 `sys.flags` 的类型 `__module__` 报
  `"builtins"`，`str(type(sys.version_info))` 为 `<class 'version_info'>`；CPython 报 `'sys'`
  与 `<class 'sys.version_info'>`。错误消息与实例 repr 中的全名（`cannot create
  'sys.version_info' instances`、`sys.version_info(major=3, ...)`）与 CPython 一致。
- `types` 模块整体不可用；`sys.implementation` 背后的 `SimpleNamespace` 类型可经
  `type(sys.implementation)` 间接取得，名字本身暂无处挂载。
- 内建函数为高频子集，共 44 个。`classmethod` 等少数 CPython 内建未暴露；`__import__`
  已提供，但 `import` 语句本身仍由编译器与虚拟机处理，不依赖该函数。
- 无字节码缓存：不产生也不读取 `.pyc`，每次 import 都重新编译。
- `multiprocessing`、`subprocess` 等进程级模块未实现。

## 编译前端

编译前端（词法、语法、语义分析、字节码发射）对 CPython 合法程序存在少量拒绝性差异：以下语法在
CPython 3.14 下可编译，PySharp 在 `compile()` 阶段即报错。以 CPython 3.14.6 真实源码做过只编译
不执行的扫描：标准库 `Lib` 根层 153 个模块全部通过；`Lib/test` 顶层 448 个测试文件中 438 个通过，
失败的 10 个文件拆成语句级片段（CPython 合法片段共 13,627 个）后有 92 个被 PySharp 拒绝，
归为六类缺口；其中类基类列表的 `*`/`**` 解包、序列模式的匿名 starred（`*_`，解析时漏消费
`_`，有名 starred 任意位置本就可编译）、调用实参与下标中 `*` 后的表达式层级（`bitwise_or`
升至完整 `expression`，对齐 CPython `starred_expression` 规则，显示与 `return` 位置的
`bitwise_or` 层级保留）三类已修复，余下三类：

- f-string 的部分高级形态：表达式内三引号字面量（`f"{'''x'''}"`）、多行表达式含注释、
  format spec 内多个嵌套替换字段（`{value:{w:0}.{p:1}}`）、raw f-string 的 `\{{` 转义。
- 推导式与生成器表达式内的 `async` 上下文误判：genexp 中的 `async for`、genexp 过滤条件里的
  `await`（后者在 async 函数内合法，PySharp 误报 `'await' outside async function`）。
- 函数内推导式中的 `yield`：`def g(): [x for x in [(yield 1)]]`。

## 运行时与语言细节

- 错误消息文本：异常类型与层级与 CPython 一致。消息措辞为独立实现（资源串集中在 `PySR`），
  以 CPython 为基准持续对齐，已完成运算符报文、str 方法族 `TypeError`、容器构造参数校验、异常链
  属性删除报文、int 转换位数限制提示、super 与 isinstance 族分支报文、复合语句头部缺冒号的
  `expected ':'`、导入失败消息的模块名 repr 等大批量对齐，但逐字对照 CPython 的测试仍可能有零星
  出入。
- `id()` 与 `hash()` 的具体数值不与 CPython 对齐。`id` 为运行时分配的稳定标识，`hash` 经内部算法
  归一化。依赖具体数值的脚本不受支持，但同一实现内的哈希不变量（如相等整数哈希相等）有回归保障。
- 线程模型：`threading.Thread` 映射为 .NET 托管线程，解释器在环境退出时以中断加等待收尾，与
  CPython 的 daemon 线程语义不完全一致。没有 GIL，跨线程共享可变内建对象（dict 是非线程安全
  实现）需自行加锁。
- `threading` 模块状态的生命周期：模块的运行时状态（活跃线程注册表、主线程对象、`stack_size()`
  配置）归属于执行环境而非模块对象，不随模块重建重置。`del sys.modules['threading']` 后重新
  import 得到新模块对象，但 `active_count()`、`enumerate()`、`main_thread()` 与运行中线程的
  `current_thread()` 仍报告原有线程与同一主线程实例。CPython 中这些状态放在模块对象上，重载后
  全部重置：旧线程从注册表消失、`main_thread()` 返回新实例、失联线程的 `current_thread()`
  返回 `_DummyThread`，重新 import `_thread` 后连 `stack_size()` 配置也归零。重载 threading
  在 CPython 中同样是病态用法（线程注册全部丢失、运行中的线程降级为 dummy），本差异按环境
  隔离性优先取舍。另外 CPython 的 `threading` 是纯 Python 模块、底层为 C 模块 `_thread`；
  PySharp 无独立 `_thread` 模块，C 层设施（`lock`、`RLock`、`_local` 类型与 `get_ident` 等
  函数）直接由 `threading` 提供。
- 外来线程的 `_DummyThread` 不过期：未经 `threading.Thread` 启动的线程首次调用
  `current_thread()` 时注册的 `_DummyThread` 不会随线程退出而出册，CPython 借 `_thread._local`
  的析构自动移除。同一环境内多个外来宿主线程先后触碰会让 `enumerate()` 与 `active_count()`
  逐渐累积。
- `threading.local` 的属性错误消息不带模块前缀：报 `'_local' object has no attribute ...`，
  CPython 为 `'_thread._local' object ...`；类型名、`__module__` 与 `<class '_thread._local'>`
  显示两侧一致。
- GC 语义：对象生命周期由 .NET GC 管理，`__del__` 的时机与 CPython 引用计数驱动的方式不同，
  析构时机不保证。
- 缓冲协议：`memoryview` 是底层 `bytes` / `bytearray` 的活视图——视图写入落入导出方、导出方的
  修改对视图可见，与 CPython 一致；导出存活期间 bytearray 的一切变长操作报 `BufferError`
  （同尺寸写入不受限），视图 `release()` 或 `with` 退出后解锁。与 CPython 的差异源于无引用计数且
  无析构钩子：被丢弃（不再引用但未 `release()`）的视图会持续占用导出，GC 回收也不会解锁，该
  bytearray 在进程存续期内无法再变长；CPython 在引用归零时立即释放。显式释放的代码两侧行为一致。
- 文件对象的文本模式定位：`seek` 在文本模式下对非零的相对定位（`whence=1` 或 `whence=2`）报
  `_io.UnsupportedOperation`，与 CPython 的 `TextIOWrapper` 一致；绝对定位受支持。
- 标准流与文件对象同类型：`sys.stdin` / `sys.stdout` / `sys.stderr` 与文本模式 `open()` 的返回值
  同为 `_io.TextIOWrapper`（`__module__` 为 `_io`，`type(...).__name__` 为 `TextIOWrapper`），
  `type(sys.stdout) is type(open(...))` 成立，成员面与 dunder 协议（`with`、迭代）一致。仍存的差异是
  `io` / `_io` 模块本身未接入，故 `isinstance(x, io.TextIOWrapper)` 无从书写，见
  [标准库模块覆盖](./stdlib-modules.md)。
- 标准流的编码：`sys.stdout.encoding` 等取自宿主环境的标准流编码（控制台宿主为
  `Console.OutputEncoding`，测试宿主默认 UTF-8），与 `open()` 的 `encoding` 参数同源。CPython 的
  `PYTHONIOENCODING` 环境变量尚未支持。
- 标准流的可定位性：`sys.stdout.seekable()` 等委托底层句柄。CPython 在 stdout 重定向到文件或 NUL
  时报告 `True`（`seek`/`tell` 可用），而 .NET 的控制台流包装（`WindowsConsoleStream`）无论是否重定向
  都报告 `CanSeek` 为 `False`，故 PySharp 一律报告 `False`，`seek`/`tell` 抛
  `_io.UnsupportedOperation: underlying stream is not seekable`。
- 标准流的缓冲与 CPython 的 `create_stdio` 对齐：`sys.stdout` 在终端上逐行缓冲、重定向到管道或文件时
  按 8192 字节块缓冲，`sys.stderr` 无论是否重定向都逐行缓冲，缓冲内容在 `flush()`、`close()` 与解释器
  退出（对齐 `flush_std_files`，flush 当时绑定的 `sys.stdout`/`sys.stderr`）时落盘。因此合并捕获
  （`2>&1`）中 stderr 行先于全部 stdout 行出现，与 CPython 一致。`sys.stdout.isatty()`、
  `line_buffering` 与 `write_through`（只读）按装配时的缓冲模式报告。`-u` 与 `PYTHONUNBUFFERED`
  开关尚未支持。
- 文本流成员面尚缺 `fileno`、`buffer`、`detach`、`newlines`、
  `reconfigure`、`truncate`、`writelines`；这些在标准流与 `open()` 的文本对象上同样缺失
  （两类型共同的缺口，非二者之间的不对称）。
- `open()` 支持的参数与 CPython 对齐，包括 `buffering`、`encoding`、`errors` 与 `newline`；
  文本模式默认 `newline=None`，读入时做通用换行归一，写出时展开为平台换行符。详见
  [文件对象](../user-guide/file-objects.md)。

## 嵌入 API

对 C# 使用者：

- 无动态互操作层：不提供对任意 C# 对象的 `dynamic` 风格反射绑定。C# 与 Python 的值交换通过强类型
  `Py*Object` API 完成，见
  [在 C# 中操作 Python 对象](../user-guide/python-objects-from-csharp.md)。
- `PyCallContext` 无公共构造：调用、属性与协议操作 API 需要上下文，当前仅在扩展点（自定义类型
  或模块方法的实现）内由运行时提供。扩展实现可经上下文的 `PyEnvironment` 公共属性访问所在环境，
  读取注入的环境数据，见[环境数据注入](../user-guide/environment.md#环境数据注入)。
- 读取模块全局变量无公共 API：`RunCode` 与 `RunFile` 返回的模块对象暂不能从 C# 侧直接取属性。
  取回数据的方式是重定向 stdout，或抛出携带数据的异常，见
  [执行 Python 代码](../user-guide/executing-python.md#当前限制)。

## 工具链

- REPL：多行续行判定、表达式回显、`builtins._` 绑定上次结果、`sys.path[0]` 预置当前目录都与
  CPython 一致。`sys.exit` 在 REPL 中被解释器捕获，退出码记入环境，会话继续，以 EOF 结束会话。
  逐项行为见[命令行与交互模式](../user-guide/cli-and-repl.md)。
- CLI：`pysharp` 支持 `-h` 与 `--help`、`-V` 与 `--version`、`-c`、`-O` 与 `-OO`、`-S`、
  `-`（stdin 模式）与 `--`（选项终止符），脚本参数透传，无参数时进入 REPL。尚不支持 CPython 的
  `-m` 与 `-i`。未捕获异常的 traceback 是否带 ANSI 颜色由宿主决定。

## 相关节点

- [语言特性支持清单](./language-features.md)
- [标准库模块覆盖](./stdlib-modules.md)
