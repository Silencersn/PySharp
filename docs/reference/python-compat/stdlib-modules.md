# 标准库模块覆盖

PySharp 的标准库以 C# 内嵌模块为主，注册于 `PyStandardLibrary`，无需磁盘文件。此外虚拟文件系统
支撑纯 Python 模块的加载，即 `sys.path` 上的 `.py` 文件与包，见
[虚拟文件系统](../user-guide/virtual-file-system.md)。内嵌模块共 13 个：

| 模块 | 提供内容 |
| --- | --- |
| `builtins` | 44 个内建函数（见下）、全部内建类型与 68 个异常类型（含警告族 12 个，即 `Warning` 基类加 11 个子类） |
| `sys` | `argv`、`stdin`、`stdout`、`stderr`（`OnImport` 时以 `PyTextIOWrapperObject` 流包装注入，与文本模式 `open()` 同类型，条目见[内建类型速查表](../api/builtin-types.md)）；`path`（活动列表，导入机器实时读取，`append`/`insert`/`remove` 即时影响后续 import）、`modules`（权威导入注册表：删除条目强制重载、赋值条目在 import 时原样返回、`None` 条目令 import 抛 `ImportError: import of X halted; None in sys.modules`）；`exit()` 与 `getrecursionlimit()` / `setrecursionlimit(n)`；信息量成员 `version`、`version_info`、`flags`、`implementation`、`platform`、`byteorder`、`executable`、`prefix`、`maxsize`、`maxunicode`、`dont_write_bytecode`、`builtin_module_names`；`getdefaultencoding()`、`intern()`、`excepthook`（可替换，未捕获异常经其报告）；`get_int_max_str_digits()` 与 `set_int_max_str_digits(n)`（见下文）。`version_info` 与 `flags` 是 CPython structseq 形态的 tuple 子类（字段名可读、不可实例化、structseq repr），`implementation` 以 `SimpleNamespace` 形态承载（见下文） |
| `site` | `exit`、`help` |
| `operator` | 19 个运算函数：`add`、`sub`、`mul`、`truediv`、`floordiv`、`mod`、`pow`、`lshift`、`rshift`、`and_`、`or_`、`xor`、`lt`、`le`、`eq`、`ne`、`gt`、`ge`、`length_hint` |
| `math` | 常量 `pi`、`e`、`tau`；29 个函数：`sqrt`、`acos`、`asin`、`atan`、`atan2`、`cos`、`sin`、`tan`、`acosh`、`asinh`、`atanh`、`cosh`、`sinh`、`tanh`、`exp`、`fabs`、`ceil`、`floor`、`trunc`、`remainder`、`copysign`、`fmod`、`pow`、`gcd`、`lcm`、`log`、`log2`、`log10`、`log1p` |
| `time` | `time()` |
| `random` | `random()`、`uniform(a, b)`、`randrange(...)`、`randint(a, b)` |
| `threading` | `Thread` 类：`start`、`join`、`run`、`is_alive`、`name`、`ident`、`native_id`、`daemon`（含 `getName`/`setName`/`isDaemon`/`setDaemon` 旧接口）及其 `Timer` 子类；同步原语 `Lock`、`RLock`、`Condition`、`Event`、`Semaphore`、`BoundedSemaphore`、`Barrier` 与异常 `BrokenBarrierError`；线程局部存储 `local`；函数 `current_thread`、`active_count`、`enumerate`、`main_thread`、`get_ident`、`get_native_id`、`stack_size`、`excepthook`（可替换，工作线程未捕获异常经其报告）；常量 `TIMEOUT_MAX` 与 `ThreadError`（即 `RuntimeError`）。与 CPython 的行为差异见[与 CPython 的差异](./cpython-differences.md) |
| `queue` | `Queue` 类：`qsize`、`empty`、`full`、`put`、`put_nowait`、`get`、`get_nowait`、`task_done`、`join` |
| `typing` | `Generic` 基类、`GenericAlias`、`TypeVar`、`TypeAliasType` 等泛型运行时设施 |
| `this` | Python 之禅，冻结模块，源码在编译期嵌入 |
| `dataclasses` | `@dataclass` 装饰器与 `field()`，冻结模块，见下文 |
| `warnings` | 警告机制：`warn`、`warn_explicit`、`filterwarnings`、`simplefilter`、`resetwarnings`、`catch_warnings`，以及 `deprecated` 装饰器，见下文 |

另有 t-string 的运行时支撑类型 `Template` 与 `Interpolation`，归属 `string.templatelib` 命名，
配合 PEP 750 模板字符串使用。

## builtins 函数清单

`__import__`、`abs`、`aiter`、`all`、`anext`、`any`、`ascii`、`bin`、`breakpoint`、
`callable`、`chr`、`compile`、`delattr`、`dir`、`divmod`、`eval`、`exec`、`format`、`getattr`、
`globals`、`hasattr`、`hash`、`hex`、`id`、`input`、`isinstance`、`issubclass`、`iter`、`len`、
`locals`、`max`、`min`、`next`、`oct`、`open`、`ord`、`pow`、`print`、`repr`、`round`、
`setattr`、`sorted`、`sum`、`vars`，共 44 个。

`enumerate`、`zip`、`map`、`filter`、`reversed` 以内建类型的形式提供，与 CPython 一致。
类型构造（`int()`、`str()`、`list()` 等）经类型对象调用完成。

## warnings

自订 `Warning` 子类可作为过滤类别。

| 成员 | 说明 |
| --- | --- |
| `warn(message, category=None, stacklevel=1)` | `message` 为 `Warning` 实例时以其实例类别为准；`stacklevel` 经 `__index__` 归化，过大报 `OverflowError` |
| `warn_explicit(message, category, filename, lineno, ...)` | 显式定位版，形参集与 CPython 一致 |
| `filterwarnings(action, message='', category=None, module='', lineno=0, append=False)` | 插入或追加一条过滤器 |
| `simplefilter(action, category=None, lineno=0, append=False)` | 不按消息文本匹配的简化过滤器 |
| `resetwarnings()` | 重置为默认过滤器 |
| `catch_warnings(record=False, ...)` | 上下文管理器，保存并恢复过滤器与显示状态；`record=True` 时以列表收集警告。`module` 参数未实现，传入报 `NotImplemented` |
| `deprecated(message, *, category=None, stacklevel=1)` | PEP 702 装饰器，装饰类或函数，运行期使用时发警告；`category=None` 仅打标不告警 |

过滤动作全集为 `"default"`、`"error"`、`"ignore"`、`"always"`（别名 `"all"`）、`"module"`、
`"once"`，匹配维度是动作、消息正则、类别、模块正则与行号。过滤与去重状态是每解释器的，绑定在环境
上，跨环境互不影响。`catch_warnings(record=True)` 产生的条目对象暴露 `message`、`category`、
`filename`、`lineno`、`file`、`line`、`source` 属性。

## dataclasses

冻结模块，源码 `PySharp/Lib/dataclasses.py` 在编译期嵌入，提供 `@dataclass`：

- `@dataclass(cls=None, *, init=True, repr=True, eq=True, frozen=False, order=False,
  match_args=True, kw_only=False)` 按类注解的字段自动生成 `__init__`、`__repr__`、`__eq__` 与
  `__match_args__`。`frozen=True` 与 `order=True` 尚未支持，传入即报 `TypeError`。
- `field(*, default=..., default_factory=..., init=True, repr=True, compare=True, hash=None,
  metadata=..., kw_only=...)` 提供字段级控制。`default` 与 `default_factory` 同时给出报
  `ValueError`；`metadata` 为只读视图；`kw_only` 与类级 `kw_only`、`KW_ONLY` 标记协同。
- 继承字段按逆 MRO 合并，基类字段在前、子类覆盖；`__post_init__` 由生成的 `__init__` 在尾部调用
  并传入全部 InitVar 值；`InitVar` 伪字段占据参数、不写实例、排除在 `repr` 与 `eq` 之外。
- 未覆盖：`asdict`、`astuple`、`replace`，访问时报 `AttributeError`。

用法篇见[警告与数据类](../user-guide/warnings-and-dataclasses.md)。

## sys 的信息量成员

- `version_info` 与 `flags` 按 CPython structseq 形态实现：tuple 子类，字段名（`major`/`minor`/
  `micro`/`releaselevel`/`serial`，`flags` 的 18 个开关字段）经 getset 描述符可读，直接实例化报
  `TypeError: cannot create ... instances`，repr 为 `sys.version_info(major=3, ...)` 形态。
  序列协议（索引、切片、比较、迭代）继承自 tuple。
- `flags` 中 `dont_write_bytecode` 恒为 1（PySharp 从不写字节码缓存，等价常开 `-B`），
  `int_max_str_digits` 动态读取当前限制，`set_int_max_str_digits` 的调用会反映到该字段。
  `utf8_mode` 恒为 0，不读取 `PYTHONUTF8` 环境变量。
- `implementation` 是 attribute bag（CPython 3.14 起同为 `SimpleNamespace` 形态），字段有
  `name`（`"cpython"`，与 `version_info`/`cache_tag`（`cpython-314`）/`hexversion` 自洽）、
  `version`（与 `sys.version_info` 同对象）、`supports_isolated_interpreters`。类型对象的
  Python 可见名属 `types` 模块，但 `types` 模块本身尚不可用，名字暂不可直接访问。
- `sys.exit(code)` 对任意 `code` 抛 `SystemExit`，退出码由顶层在异常冒泡时解析：整数透传
  （含截断语义），字符串与多参元组打印到 stderr 并以状态 1 退出，`None` 为 0。`site` 的
  `exit` / `quit` 转发同一实现。
- `sys.excepthook` 默认与内建报告一致，可整体替换（CPython 的 `PyErr_PrintEx` 通道）：
  未捕获异常以 `(type, value, None)` 调用钩子（PySharp 无 traceback 对象，第三参为 `None`），
  替换后的钩子自身抛错时按 CPython 的两段式报告（`Error in sys.excepthook:` 加
  `Original exception was:`）。原始内建钩子保留在 `sys.__excepthook__`。

## 整数字符串转换位数限制

对齐 CPython 3.11 及以后的 `int_max_str_digits` 机制，防御整数与十进制字符串互转的二次方复杂度
攻击：

- 作用范围：`int(string)`（含 `int(s, base)`）解析、`str(int)` 与 `repr` 输出、源码中的整数字面量
  （超限报 `SyntaxError`，提示改用十六进制字面量），以及运算结果超限时的字符串化。
- 默认上限 4300 位。`sys.set_int_max_str_digits(n)` 调整，`n = 0` 表示禁用限制，
  `0 < n < 640` 报 `ValueError`（640 是 CPython 规定的最小有效值），非 int 实参报 `TypeError`，
  超出 `int32` 报 `OverflowError`。
- `sys.get_int_max_str_digits()` 读取当前值。该限制是进程级全局状态，跨环境共享；与 CPython 的
  解释器级语义在单进程多环境场景下有差异。

## 文件与 import

- `open()` 基于环境宿主的虚拟文件系统（内存 FS 或物理 FS），支持读、写、追加、二进制等常用模式，
  并可指定 `buffering`、`encoding`、`errors` 与 `newline`，见[文件对象](../user-guide/file-objects.md)。
- `import` 的解析顺序是内嵌模块注册表，然后按 `sys.path` 目录扫描文件系统。包（含 `__init__.py`）
  与相对导入已支持。`.pyc` 缓存等机制未实现。

## 相关节点

- [语言特性支持清单](./language-features.md)
- [与 CPython 的差异](./cpython-differences.md)
