# Environments 与模块解析

源码：`PySharp/Runtime/Environments/`、`PySharp/Runtime/PyStandardLibrary.cs`、
`PySharp/Runtime/IO/`。

环境子系统回答两个问题：一次 Python 会话依赖哪些宿主资源（`PyEnvironmentHost` 与
`PyEnvironment`），以及 `import` 如何找到模块（提供器链与文件系统）。公共 API 的使用面见
[配置执行环境](../user-guide/environment.md)与
[虚拟文件系统](../user-guide/virtual-file-system.md)，本篇从内部视角梳理接线。

## 对象关系

```text
PyEnvironmentHost（abstract）
    ├─ AllocateStdIn / AllocateStdOut / AllocateStdErr : Stream
    ├─ DefaultEncoding : Encoding
    ├─ SupportsColorOutput / SupportsErrorColorOutput : bool
    └─ FileSystem : IVirtualFileSystem

PyEnvironment
    ├─ Host                : PyEnvironmentHost
    ├─ InternPool          : 字符串驻留池
    ├─ Modules             : Dictionary<string, PyModuleObject?>（sys.modules 出现前的内部注册表）
    ├─ Paths / Args        : 构建期搜索路径回退值与 sys.argv
    ├─ Threads             : ConcurrentSet
    ├─ ModuleProviders     : List<PyModuleProvider>（Builtin 然后 Path）
    ├─ EnvData             : ConcurrentDictionary（宿主注入数据，对 Python 不可见）
    ├─ Options             : PyEnvironmentOptions
    ├─ Warnings            : WarningState
    ├─ RecursionLimit      : int（sys.get/setrecursionlimit 读写）
    └─ ExitCode            : int（internal）
```

`PyEnvironmentHostBuilder` 的实现类是配置型宿主。`PyEnvironment` 在构造时分配流并按编码包装，
经 `Host.FileSystem` 访问文件系统，经 `ModuleProviders` 解析模块。

- `PyEnvironment` 的构造函数为 `internal`：从宿主一次性分配三条 `Stream` 并按编码包装
  （`stdinEncoding ?? Host.DefaultEncoding`，stdout 与 stderr 的 `AutoFlush` 为 `true`）。
  `ModuleProviders` 默认初始化为 `[Builtin, Path]`，顺序即解析顺序；环境构建器可用
  `AddModuleProvider`（追加链尾）、`InsertModuleProvider`（插链头）与 `ClearModuleProviders`
  （清空自建）定制完整链。`Options`（`NotImplyImportSite`
  与 `OptimizationLevel`）与 `Warnings`（每解释器的警告策略状态，见
  `Runtime/WarningState.cs`）随构造就位。
- 环境数据注入：`SetEnvData` / `TryGetEnvData` / `RemoveEnvData` 按键存取宿主对象，存储是随环境
  构造的私有 `ConcurrentDictionary`。注入值对 Python 结构性不可见——不进任何模块或内建的属性
  字典，只有拿到 `PyCallContext` 的扩展点经其 `public` 访问器 `context.PyEnvironment` 才能读，
  构成宿主向内建代码传值又不暴露给脚本的沙箱边界。环境 `Dispose` 不释放注入值，宿主用
  `OnDisposing`（释放前钩子，见下）自行回收。
- 生命周期：`Dispose` 先触发一次 `OnDisposing`（订阅者异常不阻断清理，但会在清理完成后从
  `Dispose` 抛出），随后 `Interrupt` 并 `Join` 全部登记线程（`Threads` 由 `threading`
  模块注册），最后释放三条标准流。退出码存于环境的 `ExitCode`（`internal`）。

## import 解析流程

入口是 `InternalTryLoadModule(context, qualifiedName, ...)`（`PyEnvironment.Import.cs`）：

1. 缓存：sys 模块已加载时以 `sys.modules` 字典为权威缓存（对应 CPython 机制读 `interp->modules`）：
   命中即返回原条目——赋值的任意对象都会在 import 时原样拿到（不校验类型），值为 `None` 则抛
   `ImportError: import of X halted; None in sys.modules`，删除即强制走重新加载。sys 模块自身
   加载完成前回退内部 `Modules` 字典（引导期没有 Python 代码能碰 `sys.modules`）。
2. 点分名拆解：`a.b.c` 先加载根 `a`，再沿每段的 `__path__`（包搜索路径列表）逐级加载子模块，
   并把子模块绑定到父模块属性。
3. 根模块：搜索路径取 `sys.path` 属性本身（`GetRootSearchPaths`，对应 CPython 的
   `PySys_GetObject("path")`），Python 侧对 `sys.path` 的 `append`/`insert`/`remove` 即时影响
   下一次查找；sys 未加载时回退构建期 `Paths`。进入提供器链逐个尝试。非根模块使用父包的
   `__path__`，而不是 `sys.path`。
4. 加载分两个阶段（对齐 CPython PEP 451 与 `_load_unlocked`）：提供器先 `TryCreateModule`
   定位并构造模块，机制层随即把模块登记进 `sys.modules` 与内部 `Modules`（`RegisterLoadedModule`
   同步写两处），再调用提供器的 `ExecModule` 执行模块体，最后调用模块的 `OnImport`。初始化期间
   模块已在缓存里，循环导入看到的是半初始化对象而不会重入提供器；初始化抛异常时机制层回滚登记
   （两处同删），下一次 import 从头重试。整个流程都在
   `CreateModuleFrame(isRoot: false)` 的新模块帧中执行。

两个内建提供器（`PyModuleProvider.cs`）：

- `BuiltinModuleProvider`：`TryCreateModule` 查询 `PyStandardLibrary.TryCreateModule`，即一张
  硬编码注册表（`builtins`、`site`、`operator`、`math`、`time`、`random`、`this`、`dataclasses`、
  `threading`、`queue`、`typing`、`sys`、`warnings`）。注册表产出即完整模块（单阶段形态），
  `ExecModule` 用默认空实现。
- `PathProvider`：`TryCreateModule` 对搜索路径逐项定位——目录存在且名为 `<name>` 则为包（有
  `__init__.py` 时设 `__file__` 并记下待执行体，没有时是 namespace 包，`__file__` 为 `None`），
  `<name>.py` 存在则设 `__file__` 并记下文件；`ExecModule` 覆写把 `PySourceDecoder.Decode` 出的
  源码经 `RunCodeWithContext(isMain: false)` 执行为模块体。全部内容来自 `Host.FileSystem`。

`MappingModuleProvider`（由 `PyModuleProvider.Create` 构造）是公共的自定义提供器实现，
字典命中即调用工厂，同样走默认 `ExecModule`。

### OnImport 时序

`OnImport` 由机制层在 `ExecModule` 成功返回后统一调用，调用时模块已登记进 `Modules`：

- `Mapping` 与 `Builtin`：工厂或注册表产出即完整模块，`OnImport` 是唯一的导入后钩子。
- `Path`：普通模块与常规包先执行模块体（`ExecModule`），再触发 `OnImport`；namespace 包没有
  模块体，只触发 `OnImport`。基类的 `OnImport` 是空实现，所以文件模块默认无可观察钩子。真实的
  覆写点只有 `site` 的 `exit` 与 `help` 注入、`sys` 的 `argv`、`path`、`modules` 与三条流包装，
  以及冻结模块的编译执行。
- 冻结模块（`PyFrozenModuleObject.OnImport`）：`CodeObject ??=` 只缓存编译产物，
  `InternalExecuteToModule` 每次导入都会重新执行冻结源码。每环境首次导入各执行一遍，跨环境不共享
  执行状态，共享的只是编译后的指令。
- 与 CPython 的对齐点：机制层在初始化前登记模块（字面写入 `sys.modules`，即 CPython 的先注册再
  `exec_module`）、失败回滚（对应 `del sys.modules[name]`）。`site` 等经 `LoadBuiltinModule`
  直取注册表的路径遵循同一时序（先登记再 `OnImport`）。

相对导入由 `ResolveRelativeModuleName` 实现（PEP 328 的 `resolve_name`）：以调用帧 globals 的
`__package__`（回退到 `__name__` 加 `__path__` 判定）为基准，按 `level` 上溯包层级再拼接。

## `__main__` 与模块执行

- `PyInterpreter` 持有 `__main__` 模块与解释器根上下文。根帧变量与 `__main__` 的属性字典是同一份
  globals（`MergeThenReplaceGlobals`），模块级赋值直接落入模块属性。
- `RunCodeWithContext` 与 `InternalExecuteToModule` 负责普通模块执行：设置 `__name__` 与
  `__package__`，执行，然后断言 globals 回链。
- `site` 的隐式导入由环境初始化触发，可用 `NotImplyImportSite` 关闭。`site` 模块提供 `exit` 与
  `help`。

## 模块属性访问与 PEP 562 钩子

`PyModuleObject.GetAttr` 覆写的解析顺序是：命名空间（模块属性字典）命中则返回；未命中且模块字典
定义了 `__getattr__(name)` 时，以属性名调用钩子并原样返回结果，异常不做加工（`AttributeError`
即「属性缺失」信号，其他异常向上传播），与 CPython 的 `module_getattro` 透传一致；无钩子时报
`AttributeError: module '<name>' has no attribute '<attr>'`。`LOAD_ATTR`、`getattr()`、
`hasattr()` 与 `from ... import` 全部经过该路径，因此 `from m import x` 对未导出名报的
`ImportError` 正是钩子抛出的 `AttributeError` 被导入机制归化的结果。

模块的 `__dir__` 方法：优先无参调用模块字典的 `__dir__` 钩子，否则返回模块属性字典的键列表。
`dir()` 内建函数改为解析类型上的 `__dir__` 绑定调用，并对结果列表化排序；无 `__dir__` 的类型保持
「实例字典与 MRO 合并」的缺省。排序由文化敏感序改为码点序，与 `sorted()` 一致。

## 文件系统层

`IVirtualFileSystem` 的四个核心成员与接口默认方法（`ReadAllText` 等）详见
[PyFileSystem 参考](../api/PyFileSystem.md)。内部实现要点：`MemoryFileSystem` 以目录到子目录、
目录到文件、文件到数据三组字典加根集合维护树，文件数据 `MemoryFileData` 支持并发流打开与
`FileShare` 语义；`PhysicalFileSystem` 包装 `System.IO`。`open()` 内建函数与 `PathProvider`
都只依赖该抽象，这是沙箱与内存脚本的实现基础。

## 相关阅读

- [threading 与 queue](./threading-and-queue.md)：线程集 `Threads` 的登记与 Interrupt 及 Join
  收尾的消费方
