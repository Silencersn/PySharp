# 标准库模块覆盖

PySharp 的标准库以 C# 内嵌模块为主，注册于 `PyStandardLibrary`，无需磁盘文件。此外虚拟文件系统
支撑纯 Python 模块的加载，即 `sys.path` 上的 `.py` 文件与包，见
[虚拟文件系统](../user-guide/virtual-file-system.md)。内嵌模块共 13 个：

| 模块 | 提供内容 |
| --- | --- |
| `builtins` | 44 个内建函数（见下）、全部内建类型与 68 个异常类型（含警告族 12 个，即 `Warning` 基类加 11 个子类） |
| `sys` | `argv`、`stdin`、`stdout`、`stderr`（`OnImport` 时以 `PyStdIoObject` 流包装注入，类型条目见[内建类型速查表](../api/builtin-types.md)）、`get_int_max_str_digits()` 与 `set_int_max_str_digits(n)`（见下文）。`version`、`flags`、`modules`、`path` 等对 Python 侧不可见，见[常见问题](../user-guide/faq.md) |
| `site` | `exit`、`help` |
| `operator` | 19 个运算函数：`add`、`sub`、`mul`、`truediv`、`floordiv`、`mod`、`pow`、`lshift`、`rshift`、`and_`、`or_`、`xor`、`lt`、`le`、`eq`、`ne`、`gt`、`ge`、`length_hint` |
| `math` | 常量 `pi`、`e`、`tau`；29 个函数：`sqrt`、`acos`、`asin`、`atan`、`atan2`、`cos`、`sin`、`tan`、`acosh`、`asinh`、`atanh`、`cosh`、`sinh`、`tanh`、`exp`、`fabs`、`ceil`、`floor`、`trunc`、`remainder`、`copysign`、`fmod`、`pow`、`gcd`、`lcm`、`log`、`log2`、`log10`、`log1p` |
| `time` | `time()` |
| `random` | `random()`、`uniform(a, b)`、`randrange(...)`、`randint(a, b)` |
| `threading` | `Thread` 类：`start`、`join`、`run`、`is_alive` |
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
