# 类型映射与转换

源码：`PySharp/Modules/Builtins/` 各值类型、`PySharp/Runtime/Calls/PyDelegates.cs`。

PySharp 没有动态互操作层：不存在「C# `int` 自动变 Python `int`」的隐式转换，也不包装任意 .NET
对象。每个 Python 值在 C# 侧都是强类型的 `Py*Object`，交换只有三种显式动作：

1. 构造（C# 到 Python）：静态工厂与单例。
2. 读取（Python 到 C#）：公共属性与访问器。
3. 协议操作（Python 语义）：调用、下标、属性、运算符，需要 `PyCallContext`，仅扩展点内提供，见
   [在 C# 中操作 Python 对象](./python-objects-from-csharp.md#协议操作与-pycallcontext)。

扩展方法的形参与返回值同样遵循此规则：签名必须使用 `PyObject` 族类型，.NET 原生类型
（`int`、`string`、`double` 等）不会被自动转换，参数绑定与校验（`PyArgsDef`、`PyArgsValidator`）
全部在 `PyObject` 域内完成。

## 完整映射表

| Python 类型 | C# 值类型 | 构造（到 Python） | 读取（到 C#） |
| --- | --- | --- | --- |
| `NoneType` | `PyNoneObject` | `PyNoneObject.None` | 用 `is PyNoneObject` 判定 |
| `bool` | `PyBoolObject` | `True`、`False`、`FromBoolean(bool)` | `.BoolValue : bool` |
| `int` | `PyIntObject` | `FromInteger(int)`、`(long)`、`(BigInteger)`；`Zero`、`One`、`MinusOne` | `.Value : BigInteger`；`.IsInt32` 与 `.Int32Value` |
| `float` | `PyFloatObject` | `FromDouble(double)`；`NaN`、`Pi`、`E` 等常量 | `.Value : double` |
| `complex` | `PyComplexObject` | `FromComplex(Complex)`、`FromRealImag(double, double)` | `.Value : Complex`、`.Real`、`.Imag` |
| `str` | `PyStrObject` | `FromString(string)`；`Empty` | `.Value : string`、`.PyLength : int` |
| `bytes` | `PyBytesObject` | `FromBytes(byte[])`、`(ReadOnlySpan<byte>)`；`Empty` | `this[int] : byte`、`.Length` |
| `bytearray` | `PyByteArrayObject` | `FromBytes(...)`、`CreateEmpty()` | `this[int] : byte`（可写） |
| `list` | `PyListObject` | `CreateList(params IEnumerable<PyObject>)` | `this[int]`（可读写）、`Count`、`Add` 等 |
| `tuple` | `PyTupleObject` | `CreateTuple(params ...)`；`Empty` | `this[int]`、`Count`、`AsSpan()` |
| `dict` | `PyDictObject` | `CreateDict()`、`CreateDict(context, pairs...)` | `Count`、`TryGetValue(string, out)`、`this[string]` |
| `set`、`frozenset` | `PySetObject`、`PyFrozenSetObject` | 构造函数、`CreateSet`、`CreateFrozenSet` | `Add`、`Contains`、`Count` 等集合成员 |
| `range` | `PyRangeObject` | `CreateRange(stop)`、`(start, stop, step)` | 无 |
| `NotImplemented` | `PyNotImplementedObject` | `PyNotImplementedObject.NotImplemented` | 无 |
| `Ellipsis`（`...`） | `PyEllipsisObject` | `PyEllipsisObject.Ellipsis` | 无 |

容器更细的 C# 侧成员清单见[内建类型速查表](../api/builtin-types.md)与
[在 C# 中操作 Python 对象](./python-objects-from-csharp.md#集合类型的-c-侧操作)。

## Python 类型到 C# 类型的边界

没有自动包装也意味着：

- `char` 没有对应物：Python 的单字符就是 `str`，用 `PyStrObject.FromString` 构造，`.Value` 读回
  字符串后再取字符。
- 数值不自动收窄：`int` 侧的 `FromInteger` 接受 `int`、`long`、`BigInteger`，但读取必须经
  `BigInteger`；`float` 侧即 `double`。
- 函数、类型、模块等非值类型：`PyFunctionObject`、`PyTypeObject`、`PyModuleObject` 同样是
  `PyObject`，判定与持有用 C# 的 `is` 模式匹配即可，调用它们属于协议操作，需要上下文。
- 把 C# 能力暴露给 Python 只走声明式扩展（attribute 加源生成器），见
  [扩展 PySharp](./extending-pysharp.md)，不存在「把任意 .NET 对象扔进解释器」的通道。

## 常见陷阱

- `int` 是任意精度：`((PyIntObject)obj).Value` 恒为 `BigInteger`。要 `int` 需先查 `IsInt32`，
  越界取 `Int32Value` 会抛以 `OverflowError` 为内容的 `PyRuntimeException`。
- 布尔是整数的子类，C# 侧同样如此：`PyBoolObject` 派生自 `PyIntObject`，因此
  `is PyIntObject` 对 `True` 与 `False` 会命中。判定「是否恰为 bool」用 `is PyBoolObject`。
- dict 的 `this[string]` 未命中抛 .NET 异常 `KeyNotFoundException`，不是 Python 的 `KeyError`，
  因此应先 `TryGetValue` 或 `ContainsKey`。任意对象键的下标操作走协议重载，见
  [dict 系统](../internals/dict-system.md)。
- `str` 没有 C# 索引器：取长度用 `.PyLength`，取字符先读 `.Value`。`.PyLength` 按码位计数，
  与 Python 的 `len()` 一致。
- C# 侧成员直接操作底层存储：`PyListObject.Add`、`PyDictObject.SetItem(string, ...)` 改的就是
  Python 对象本身，Python 侧立即可见，这不是拷贝。
- 值语义与引用语义：`PyIntObject` 等不可变值的相等性应经协议（`PyComparer`，扩展点内）判断，
  或先读回 C# 值再比较；C# 的 `==` 比较的是对象引用。

## 与扩展签名的衔接

`[PyMethod]` 与 `[PyExport]` 实现的参数进出遵循同一张表：入参拿到的是 `PyObject`，按需用上表读取；
返回值必须是 `PyObject` 或 `PyResult`。签名声明（`[PyFunctionParameters]`）与校验行为见
[扩展 PySharp](./extending-pysharp.md)，错误返回的写法见 [PyResult 参考](../api/PyResult.md)。

## 相关节点

[在 C# 中操作 Python 对象](./python-objects-from-csharp.md) ·
[内建类型速查表](../api/builtin-types.md) · [常见问题](./faq.md)
