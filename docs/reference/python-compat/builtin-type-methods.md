# 内建类型方法面覆盖

依据：各 `Py*Object*.cs` 的 `[PyMethod]`、`[PyClassMethod]`、`[PyStaticMethod]` 与
`[PyProperty]` 声明及协议槽。

本篇是 Python 侧方法面的覆盖清单，为每个内建类型列出已实现的方法与属性，并指出 CPython 有而
PySharp 未支持的高频成员。它回答「这个类型能点出哪些方法」，不展开语义教程；行为细节由
`test_pyfiles` 下的回归脚本守护，见[测试体系](../internals/testing.md)。未列出的成员调用即报
`AttributeError`。协议层行为（运算、比较、迭代、`len`、下标等）不在本篇，见
[语言特性清单](./language-features.md)与[协议分发](../internals/protocol-dispatch.md)。

本篇只做可机械核对的覆盖清单，不追求 CPython 标准类型文档式的全量教程，因为语义教程随版本漂移，
维护成本不可行。这与[标准库模块覆盖](./stdlib-modules.md)对模块所做的工作是对称的。

## 序列与文本

| 类型 | 已支持方法 | 属性 | CPython 有而未支持 |
| --- | --- | --- | --- |
| `str` | `capitalize`、`casefold`、`center`、`count`、`encode`、`endswith`、`expandtabs`、`find`、`format`、`index`、`isalnum`、`isalpha`、`isascii`、`isdecimal`、`isdigit`、`isidentifier`、`islower`、`isnumeric`、`isprintable`、`isspace`、`istitle`、`isupper`、`join`、`ljust`、`lower`、`lstrip`、`partition`、`removeprefix`、`removesuffix`、`replace`、`rfind`、`rindex`、`rjust`、`rpartition`、`rsplit`、`rstrip`、`split`、`splitlines`、`startswith`、`strip`、`swapcase`、`title`、`upper`、`zfill`，共 44 个 | 无 | `maketrans`、`translate`、`format_map` |
| `bytes` | `decode(encoding='utf-8', errors='strict')` | 无 | `hex`、`fromhex`、`startswith`、`endswith`、`strip` 族、`count`、`find`、`index`、`replace`、`split`、`title` 族等 |
| `bytearray` | `append`、`extend`；下标读写经 `__getitem__` 与 `__setitem__` | 无 | `decode`、`hex`、`insert`、`pop`、`remove`、`reverse`、`replace`，以及按下标删除 |
| `list` | `append`、`clear`、`copy`、`count`、`extend`、`index`、`insert`、`pop`、`remove`、`reverse`、`sort`，共 11 个，与 CPython 方法面一致 | 无 | 无 |
| `tuple` | `count`、`index`，与 CPython 一致 | 无 | 无 |
| `range` | 无方法 | 无 | `index`、`count` 与 `.start`、`.stop`、`.step` 属性。`len`、下标、迭代、`in`、`reversed` 经协议槽支持 |
| `slice` | `indices` | `start`、`stop`、`step` | 无 |
| `memoryview` | `tobytes`、`tolist`、`hex`、`release`、`toreadonly` | `obj`、`nbytes`、`readonly`、`format`、`itemsize`、`ndim`、`shape`、`strides`、`suboffsets`、`c_contiguous`、`f_contiguous`、`contiguous` | `cast`、`tobytes(order)` |

`str` 的方法面与 CPython 高频面几乎重合，仅缺 `translate` 族三个。方法的 `TypeError` 报文已逐字
对齐 CPython，覆盖 `find` 族、`partition` 族、`split` 族与 `center` 族的参数序号、类型名后缀与
`__index__` 转换报文，含 C 级的 `OverflowError` 文案。

`bytes` 与 `bytearray` 是当前覆盖最薄的类型。字节级文本处理（`split`、`replace`、`strip` 等）需先
`decode` 为 `str` 再处理。`bytes.decode` 的 utf-8 解码器逐行移植自 CPython，invalid start byte、
invalid continuation byte、unexpected end of data、超长、代理与截断的分类一致；`ascii` 报
`ordinal not in range(128)`，`latin-1` 永不失败。错误处理器惰性解析，合法输入不拒绝未知处理器名，
非法输入报 `LookupError`，`ignore`、`replace`、`backslashreplace`、`surrogateescape` 与
`surrogatepass` 全部生效。`UnicodeDecodeError` 支持 CPython 的五参构造
`(encoding, object, start, end, reason)`，属性与 `args` 可读，`str()` 按单字节与位置区间两种形态
呈现。

## 集合与映射

| 类型 | 已支持方法 | CPython 有而未支持 |
| --- | --- | --- |
| `dict` | `clear`、`copy`、`fromkeys`（classmethod）、`get`、`items`、`keys`、`pop`、`popitem`、`setdefault`、`update`、`values`，与 CPython 一致 | 无 |
| `set` | `add`、`clear`、`copy`、`difference`、`difference_update`、`discard`、`intersection`、`intersection_update`、`isdisjoint`、`issubset`、`issuperset`、`pop`、`remove`、`symmetric_difference`、`symmetric_difference_update`、`union`、`update`，共 17 个，与 CPython 一致 | 无 |
| `frozenset` | `copy`、`difference`、`intersection`、`isdisjoint`、`issubset`、`issuperset`、`symmetric_difference`、`union`，与 CPython 一致 | 无 |

字典视图（`keys`、`items`、`values` 的返回值）支持 `len`、迭代、成员判断、集合式比较与
`reversed()`，见 [dict 系统](../internals/dict-system.md)。

## 数值类型

| 类型 | 已支持方法 | 属性 | CPython 有而未支持 |
| --- | --- | --- | --- |
| `int` | 无方法 | 无 | `bit_length`、`bit_count`、`to_bytes`、`from_bytes`、`conjugate`、`numerator`、`denominator` |
| `float` | `as_integer_ratio`、`conjugate`、`fromhex`（classmethod）、`hex`、`is_integer` | `real`、`imag` | 无，与 CPython 方法面一致 |
| `complex` | 无方法 | `real`、`imag` | `conjugate` |

数值类型的运算、哈希与格式化行为见[数值系统](../internals/numeric-system.md)与
[标准库覆盖](./stdlib-modules.md)。

## 文件与其他

- 文件对象（`open()` 返回值）：`read`、`readline`、`readlines`、`write`、`seek`、`tell`、
  `close`、`flush`、`readable`、`writable`、`seekable`，属性 `closed`、`mode`、`name`，
  文本模式另有 `encoding` 与 `errors`。支持迭代与 `with`，完整签名见
  [文件对象](../user-guide/file-objects.md)。
- 生成器与协程：`send`、`throw`（含三参形态）、`close`，属性 `__name__` 与 `__qualname__`；
  守卫语义见[生成器与协程系统](../internals/generator-system.md)。
- 异常实例：`args` 及异常链属性（`__cause__`、`__context__`、`__suppress_context__`、
  `__traceback__`）支持赋值与删除，见[错误处理](../user-guide/error-handling.md)。

## 相关节点

- [标准库模块覆盖](./stdlib-modules.md)：模块函数面的同类清单
- [内建类型速查表](../api/builtin-types.md)：C# 侧类型对与构造、读取入口
- [与 CPython 的差异](./cpython-differences.md)：用户视角差异汇总
