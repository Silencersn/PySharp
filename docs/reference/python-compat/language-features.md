# 语言特性支持清单

本清单归纳 PySharp 当前支持的 Python 语言特性，依据是解释器实现与 `PySharp.Tests/test_pyfiles/`
下的 387 个回归测试脚本，这些脚本由 384 个 `[TestMethod]` 逐一驱动。未列出的特性可能部分可用，
但未经系统验证。

## 基础语法

| 特性 | 状态 |
| --- | --- |
| 变量赋值、多目标赋值、解包赋值（含扩展解包 `*`） | 支持 |
| `del` 语句（变量、下标、属性） | 支持 |
| 全部算术、位与比较运算符（含 `//`、`%`、`**`、三元 `pow` 模数） | 支持 |
| 一元运算符（`-`、`+`、`~`、`not`）、`is` 与 `is not`、`in` 与 `not in` | 支持 |
| 增强赋值（`+=` 等，含目标只求值一次） | 支持 |
| 命名表达式（海象运算符 `:=`，PEP 572） | 支持 |
| 链式赋值、链式比较 | 支持 |
| 编译期语法警告：`is` 与字面量比较、`assert` 非空元组恒真 | 支持 |
| 三引号长字符串、转义序列、字节串字面量 | 支持 |
| 源码编码声明（PEP 263）与 UTF-8 BOM，字节解码统一作用于源码加载、import、`compile`、`exec`、`eval` | 支持 |
| `%` 格式化（含浮点与前缀边界） | 支持 |
| f-string（PEP 498，含格式说明符与多种前缀组合） | 支持 |
| t-string（PEP 750 模板字符串） | 支持 |
| 格式说明符迷你语言与 `__format__` 协议：对齐、宽度、精度、类型校验，`str.format` 的转换、嵌套规格与字段访问语法 | 支持 |

## 函数

| 特性 | 状态 |
| --- | --- |
| 默认参数、关键字参数、`*args` 与 `**kwargs` | 支持 |
| 装饰器（函数与类） | 支持 |
| 闭包与自由变量（含嵌套，含与泛型混用） | 支持 |
| 函数属性 `__name__`、`__qualname__`、`__module__`、`__doc__`，含运行时改名与删除语义 | 支持 |
| 泛型函数（PEP 695 类型参数语法） | 支持 |

`__doc__` 在绑定时按 CPython 的 `_PyCompile_CleanDoc` 清理公共前导空白。

## 类与面向对象

| 特性 | 状态 |
| --- | --- |
| 类定义、继承、嵌套类、类变量 | 支持 |
| 名称改写（`_ClassName__private`） | 支持 |
| `classmethod`、`staticmethod`、`property`（含 setter 与 deleter） | 支持 |
| 特殊方法（`__init__`、`__repr__`、`__len__`、`__getitem__`、运算符等） | 支持 |
| 用户定义描述符（`__get__` 与 `__set__` 协议） | 支持 |
| 元类（含元类关键字参数） | 支持 |
| 泛型类（PEP 695 的 `class Box[T]:`、`__type_params__`、多重与嵌套类型参数） | 支持 |
| 类型别名（`type X = ...`、`TypeAliasType`） | 支持 |
| `SomeClass[int]` 下标泛型（`__class_getitem__`、`GenericAlias`） | 支持 |

## 异常

| 特性 | 状态 |
| --- | --- |
| `try`、`except`、`else`、`finally` | 支持 |
| 异常参数与异常链细节（`__cause__`、`__context__`） | 支持 |
| 异常组与 `except*`（PEP 654） | 支持 |
| 68 个内建异常类型，层级与 CPython 一致 | 支持 |

## 迭代与生成器

| 特性 | 状态 |
| --- | --- |
| `for`、`iter()`、`next()`、迭代器协议、序列协议回退 | 支持 |
| `iter(callable, sentinel)` 两参形式与耗尽语义 | 支持 |
| `enumerate`、`zip`、`reversed`、`filter`、`map` | 支持 |
| `reversed()` 作用于 dict 及其 keys、items、values 视图 | 支持 |
| 生成器（`yield`）与生成器协议 | 支持 |
| 推导式：列表、字典、集合（含异常传播） | 支持 |

## 异步

| 特性 | 状态 |
| --- | --- |
| `async def` 与 `await` | 支持 |
| `async for` | 支持 |
| 异步生成器 | 支持 |
| 异步推导式（含错误场景） | 支持 |
| `aiter()` 与 `anext()` 内建 | 支持 |

## 流程控制与其他语句

| 特性 | 状态 |
| --- | --- |
| `if`、`elif`、`else`、`while`（含 `break` 与 `continue`） | 支持 |
| 模式匹配 `match` 与 `case` | 支持 |
| `with` 语句（上下文管理器协议） | 支持 |
| 模块导入：`import`、`from ... import`、相对导入（PEP 328）、包（`__init__.py`） | 支持 |
| 全局与局部名字空间：`globals()`、`locals()`、`vars()`、`dir()` | 支持 |
| `compile()`、`exec()`、`eval()`（含 `__builtins__` 注入语义） | 支持 |

## 数据模型细节

以下行为经回归验证。

- `int`：任意精度，十进制、十六进制、八进制与二进制解析及格式化，越界与 `hash` 行为回归，
  整数字符串互转的位数上限（`sys.get_int_max_str_digits` 与 `sys.set_int_max_str_digits`，
  对齐 CPython 3.11 及以后的默认 4300 位限制）。
- `float`：格式化（`repr` 最短表示）、`round`、取模语义、`fromhex`（含 inf、nan 与次正规数）、
  `as_integer_ratio` 边界。
- `complex`：构造（含字符串实参）、一元正负、哈希与数值一致性、反射运算、幂运算主值分支。
- `str`：方法全集（`split`、`join`、`replace`、`startswith`、`endswith` 元组参数、`strip`、
  `encode` 等）与大量边界回归，`__format__` 的对齐、精度与类型规格。
- `bytes` 与 `bytearray`：字面量转义、方法、迭代、有序比较（`bytearray` 全序，`bytes` 的
  `<=` 与 `>=` 槽位），`decode` 的错误处理器全链路，默认 `strict` 抛 `UnicodeDecodeError`。
- `list`、`tuple`、`dict`、`set`、`frozenset`、`range`、`slice`、`memoryview`：方法与语义，
  含 `slice` 步长为零、排序稳定性与移植自 listsort 的比较顺序（不足 64 项二分插入，64 项及以上
  稳定归并）、非一致比较器的异常传播、哈希不变量、dict 视图的 `len` 与集合式比较等。
- 文件对象：`open()` 返回值支持 `read`、`readline`、`readlines`（含 `hint`）、`write`、`seek`、
  `tell` 等常用面。

## 相关节点

- [标准库模块覆盖](./stdlib-modules.md)
- [与 CPython 的差异](./cpython-differences.md)
- [测试体系](../internals/testing.md)
