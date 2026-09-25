# 与 CPython 的差异

PySharp 以 CPython 3 为行为参照，大量语义细节（反射协议、子类优先、协议回退、错误类型、格式化边界）
按 CPython 对齐并有回归测试覆盖，但不承诺 100% 兼容。本篇列出当前已知、对脚本作者与嵌入者可见的
差异和限制。清单不追求穷尽，具体行为以实现为准。

## 标准库与内建函数

- 标准库覆盖面有限：内嵌模块共 13 个，见[标准库模块覆盖](./stdlib-modules.md)。CPython 的大量
  模块（`os`、`re`、`json`、`collections`、`itertools`、`functools`、`pathlib` 等）尚不可用。
- `sys` 成员较少：可靠提供 `argv`、`stdin`、`stdout`、`stderr` 与
  `get_int_max_str_digits()`、`set_int_max_str_digits()`。`sys.modules`、`sys.path` 等在运行时
  内部维护，Python 侧不可访问。
- 内建函数为高频子集，共 44 个。`memoryview`、`classmethod` 等少数 CPython 内建未暴露；`__import__`
  已提供，但 `import` 语句本身仍由编译器与虚拟机处理，不依赖该函数。
- 无字节码缓存：不产生也不读取 `.pyc`，每次 import 都重新编译。
- 导入期间的模块缓存可见性：模块对象的 `OnImport`（含文件模块的源码执行）在登记进模块缓存之前
  运行，此窗口内的循环导入会重新创建或执行模块。CPython 在执行前就往 `sys.modules` 放占位模块，
  循环导入拿到的是部分初始化的模块对象。时序细节见
  [Environments 与模块解析](../internals/environments-and-modules.md)。
- `multiprocessing`、`subprocess` 等进程级模块未实现。

## 运行时与语言细节

- 错误消息文本：异常类型与层级与 CPython 一致。消息措辞为独立实现（资源串集中在 `PySR`），
  以 CPython 为基准持续对齐，已完成运算符报文、str 方法族 `TypeError`、容器构造参数校验、异常链
  属性删除报文、int 转换位数限制提示等大批量对齐，但逐字对照 CPython 的测试仍可能有零星出入。
- `id()` 与 `hash()` 的具体数值不与 CPython 对齐。`id` 为运行时分配的稳定标识，`hash` 经内部算法
  归一化。依赖具体数值的脚本不受支持，但同一实现内的哈希不变量（如相等整数哈希相等）有回归保障。
- 线程模型：`threading.Thread` 映射为 .NET 托管线程，解释器在环境退出时以中断加等待收尾，与
  CPython 的 daemon 线程语义不完全一致。没有 GIL，跨线程共享可变内建对象（dict 是非线程安全
  实现）需自行加锁。
- GC 语义：对象生命周期由 .NET GC 管理，`__del__` 的时机与 CPython 引用计数驱动的方式不同，
  析构时机不保证。
- 文件对象的文本模式定位：`seek` 在文本模式下对非零的相对定位（`whence=1` 或 `whence=2`）报
  `_io.UnsupportedOperation`，与 CPython 的 `TextIOWrapper` 一致；绝对定位受支持。
- `open()` 支持的参数与 CPython 对齐，包括 `buffering`、`encoding`、`errors` 与 `newline`；
  文本模式默认 `newline=None`，读入时做通用换行归一，写出时展开为平台换行符。详见
  [文件对象](../user-guide/file-objects.md)。

## 嵌入 API

对 C# 使用者：

- 无动态互操作层：不提供对任意 C# 对象的 `dynamic` 风格反射绑定。C# 与 Python 的值交换通过强类型
  `Py*Object` API 完成，见
  [在 C# 中操作 Python 对象](../user-guide/python-objects-from-csharp.md)。
- `PyCallContext` 无公共构造：调用、属性与协议操作 API 需要上下文，当前仅在扩展点（自定义类型
  或模块方法的实现）内由运行时提供。
- 读取模块全局变量无公共 API：`RunCode` 与 `RunFile` 返回的模块对象暂不能从 C# 侧直接取属性。
  取回数据的方式是重定向 stdout，或抛出携带数据的异常，见
  [执行 Python 代码](../user-guide/executing-python.md#当前限制)。
- 自定义模块提供器未完全接线：`PyModuleProvider.Create` 为公共 API，但挂载到环境提供器链的入口
  当前为内部实现。嵌入场景应使用内存文件系统承载自定义模块。

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
