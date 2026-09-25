# 术语表

按域分组的常用术语速查，每条给出中文术语、英文或代码标识，以及一句话定义。详细展开见各条目链接的专题篇。

## 对象模型

- **类型对（type pair）**：每个内建类型由值类型 `Py*Object` 与元类型 `Py*ObjectType` 配对组成。值类型承载数据，元类型本身也是 `PyObject`，承载类型行为。见[对象模型](./internals/object-model.md)。
- **槽（slot）**：元类型上的协议委托字段（`PyTypeSlots` 的 `Repr`、`Len`、`GetItem`、`Add` 等），是协议分发的查表目标，对应 CPython 的 `tp_*` 槽位。见[协议分发](./internals/protocol-dispatch.md)。
- **MRO（Method Resolution Order）**：方法解析顺序，类型构造时按 C3 线性化计算；属性与槽合并都沿 MRO 进行。
- **`LayoutType`（布局类型）**：类型对象记录的实例 C# 布局。用户子类化内建类型时保持布局兼容，如 `PyObjectManagedDict` 布局。见[用户类创建全流程](./internals/class-creation.md)。
- **描述符（descriptor）**：实现 get / set 协定的属性对象（`PyMemberDescriptorObject`、`PyMethodDescriptorObject`、`PyWrapperDescriptorObject` 等）。属性查找顺序“数据描述符、实例 `__dict__`、非数据描述符”由它定义。
- **附加属性（attached properties）**：无实例字典的对象经 `PyAttachedPropertiesManager` 的条件弱表（`ConditionalWeakTable`）挂接的属性；该管理器同时负责分配稳定的 `PyId`。见[对象模型](./internals/object-model.md)。
- **`PyObjectManagedDict`**：带真实每实例 `__dict__` 的对象基类（模块、异常、用户类实例）；属性字典惰性创建为 `PyDictObject`。
- **`IPyAttributesObject` / `IPyVariablesLocalsDict`**：属性字典与 locals 字典的两个 internal 最小接口，`PyDictObject` 同时实现两者。见[dict 系统](./internals/dict-system.md)。
- **`FrameLocalsProxy`**：帧局部名字空间的代理对象，由快槽与溢出字典两层桥接，实现类为 `PyFrameLocalsProxyObject`。见[dict 系统](./internals/dict-system.md)。
- **错误即值（error-as-value）**：协议层不抛异常，统一以 `PyResult`（成功值或 `PyExceptionResult`）返回的模型。见 [PyResult 参考](./api/PyResult.md)。

## 编译流水线

- **词法分析（tokenization）**：源码到 token 序列。缩进栈与 f-string 嵌套栈是 `Lexer` 的两个关键状态。见[词法分析](./internals/tokenization.md)。
- **AST（抽象语法树）**：手写递归下降 parser 的产物；名称改写（`_ClassName__private`）在 parser 层完成。见[语法分析](./internals/parsing.md)。
- **语义分析（semantic analysis）**：AST 到变量存储分类与作用域树，决定名字落到哪种存储。见[语义分析](./internals/semantic-analysis.md)。
- **快局部（fast locals）**：编译期已知局部名的槽位存储，名字映射到下标（`LocalsTable`），运行期按 span 下标存取，无字典参与。
- **cell / free 变量**：闭包捕获变量。cell 是闭包单元 `PyCellObject` 的存储，free 是引用外层 cell 的名字；`LoadDeref` / `StoreDeref` 指令族操作它们。
- **字节码（bytecode）**：2 字节指令格式（opcode 加操作数）；`BytecodeBuilder` 负责标签回填与窥孔优化。见[字节码](./internals/bytecode/README.md)。
- **`LineTable`**：指令下标到源码行的映射，traceback 定位与 `sys` 帧信息的数据源。

## 执行

- **帧（frame）**：`PyInternalFrame`（struct），含变量槽、操作数栈、指令指针与异常处理器状态；工厂方法按模块、函数、类体、推导式、`exec`、线程根分形态。见[调用与帧](./internals/calls-and-frames.md)。
- **操作数栈（operand stack）**：栈式 VM 的工作区，与快局部共用一块租借内存（`LocalsPlusMemory`）。
- **`PyCallContext`**：执行上下文，承载环境、帧栈、I/O 与比较器；没有公共构造，仅在扩展点内可用。
- **生成器三种身份**：同一 `PyGeneratorObject` 按 `PyType` 呈现为生成器、协程或异步生成器。见[生成器与协程系统](./internals/generator-system.md)。
- **同步驱动（协程）**：`await` 由 `Send` 指令同步驱动 awaitable，没有 .NET `Task` 桥接，与“协程即线程”的直觉不同。见[生成器与协程系统](./internals/generator-system.md)。
- **异常组（exception group）**：PEP 654。`ExceptionGroup` / `BaseExceptionGroup` 与 `except*` 的子集匹配语义；组结构的 C# 侧遍历器当前全为 internal，见[异常组](./internals/exception-groups.md)。
- **t-string（模板字符串，PEP 750）**：由 `Template` / `Interpolation` 对象承载的延迟插值字符串，构建期不求值。与 f-string 的差别见[f-string 与格式化](./internals/fstring-and-format.md)。
- **警告升级（warnings-as-errors）**：`simplefilter("error")` 使 `warnings.warn` 抛异常。宿主侧拦截需经 `RunCode` / `RunFile`，实例方法 `Execute` 也会在未捕获时抛出。见[警告与数据类](./user-guide/warnings-and-dataclasses.md)。
- **traceback**：异常传播时逐帧捕获的定位信息，最终渲染为 Python 风格文本（`PyRuntimeException.Message`）。

## 模块与环境

- **`PyEnvironment`**：一次 Python 会话的全部状态，含 I/O、`sys.path`、模块缓存与线程集；经 `host.CreateEnvironmentBuilder()` 构建，或用 `PyEnvironment.CreateConsole()` 等静态工厂，应 `Dispose`。
- **宿主（host）**：`PyEnvironmentHost` 是环境外部世界（标准流与文件系统）的提供者，预定义 Null / Console / Repl 与构建器。见[宿主与 I/O 重定向](./user-guide/hosting.md)。
- **虚拟文件系统（virtual file system）**：`open()` 与 `import` 的唯一 IO 通道，可选 `MemoryFileSystem`（内存）、`PhysicalFileSystem`（磁盘桥接）或自定义实现。见[虚拟文件系统](./user-guide/virtual-file-system.md)。
- **标准库模块**：编译进解释器的模块，当前共 13 个，其中 `builtins`、`math`、`sys`、`threading`、`warnings`、`queue` 等由 C# 实现。见[标准库模块覆盖](./python-compat/stdlib-modules.md)。
- **`PyStdIoObject`**：`sys.stdin` / `sys.stdout` / `sys.stderr` 的流包装对象，方法有 `read`、`readline`、`write`、`flush`、`close`、`readable`、`writable`，属性有 `closed`、`name`。见[宿主与 I/O 重定向](./user-guide/hosting.md#编码与标准流对象)。
- **`WarningState`**：每解释器的警告策略状态（internal），含过滤器列表、去重注册表与 record 收集器，随环境构造就位。见[警告与数据类](./user-guide/warnings-and-dataclasses.md)。
- **优化级（`OptimizationLevel`）**：环境选项，CLI 的 `-O` / `-OO` 累加。大于 0 时不发射 `assert` 且 `__debug__` 为 `False`；大于等于 2 时连 docstring 一并去除。
- **冻结模块（frozen module）**：以 `[PyFrozenModule]` 声明的编译期内置 Python 源码模块，当前有 `this` 与 `dataclasses`。
- **import 解析顺序**：`sys.path` 逐目录查 `<name>.py` 与 `<name>/__init__.py`；内嵌模块注册表参与解析。见[Environments 与模块解析](./internals/environments-and-modules.md)。

## 扩展与工具链

- **源生成器（source generator）**：编译期按 attribute 产出类型注册样板的组件（`Shared` 单例、`FillSlots`、方法注册），是运行时零反射、AOT 兼容的关键。见[源生成器与代码分析](./internals/source-generators.md)。
- **`Shared` 单例**：元类型的共享实例（如 `PyStrObjectType.Shared`），类型判断与工厂入口都经由它。由生成器产出，不可手写。
- **`FillSlots`**：`PyTypeObject<T>` 上填充协议槽的虚钩子，由生成代码调用。
- **`.Py.cs` 分部**：约定俗成的 partial 文件名后缀，放置 Python 侧实例方法实现，如 `PyStrObject.Py.cs` 的 `PyJoin`。
- **`AIGenerated` 分部**：源码中带 `[AIGenerated]` 标注的成员，即 AI 辅助生成后经人工核验的实现，阅读时与手写代码同等对待。
- **PYARG / PYSP / PYSPI**：分析器诊断规则族，分别对应参数注解约定（PYARG）、公共 API 使用约束（PYSP001-005）与内部实现约定（PYSPI001-009）。见[源生成器与代码分析](./internals/source-generators.md)。
- **`[PyExport]`**：把 C# 方法暴露为内建函数属性的特性，支持多重载；`PyBuiltinFunctions` 的 44 个内建函数由此组织。
- **`PySR` 资源串**：`Resources/` 下按域拆分的 const string 分部类，全部 Python 可见错误消息的文本源。见[错误消息规范](./contributing/error-messages.md)。

## 数值与字符串

- **三层驻留（interning）**：字符串的三级缓存，即 256 字符池、`PySpecialNames` 冻结字典与环境级并发字典。见[字符串系统](./internals/string-system.md)。
- **小整数池**：`[-5, 256]` 整数的缓存复用，消除热路径分配。见[数值系统](./internals/numeric-system.md)。
- **哈希归一化**：`PyHash`。整数模 2⁶¹−1；`-1` 映射为 `-2`（错误哨兵）；str 哈希即 `.GetHashCode()`。见[数值系统](./internals/numeric-system.md)。
- **整数字符串转换位数限制（int_max_str_digits）**：int 与十进制字符串互转的位数上限，默认 4300，最小 640，`0` 表示禁用；经 `sys.set_int_max_str_digits()` 调整，为进程级全局。见[标准库覆盖](./python-compat/stdlib-modules.md)。
- **编码声明（coding cookie，PEP 263）**：源码前两行注释中的 `# -*- coding: ... -*-` 声明。字节解码入口 `PySourceDecoder` 统一服务于加载、import、`compile`、`exec` 与 `eval`。见[词法分析](./internals/tokenization.md)。

## 工程与测试

- **`InternalsVisibleTo("PySharp.Tests")`**：测试工程可访问 internal 面，是调试解释器行为的关键杠杆。见[测试体系](./internals/testing.md)。
- **`test_pyfiles` 语料**：断言写在 Python 里的回归测试脚本集，当前 387 个，按主题命名；`TestPyFiles.cs` 中的 `[TestMethod]` 负责登记。见[测试体系](./internals/testing.md)。
- **Trimmable / AOT 兼容**：主库在 Debug 与 Release 均开启 `IsTrimmable` / `IsAotCompatible`；扩展模型不依赖运行时反射是其前提。
