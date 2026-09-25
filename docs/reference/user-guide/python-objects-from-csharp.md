# 在 C# 中操作 Python 对象

源码：`PySharp/Modules/Builtins/`。

PySharp 不做 `dynamic` 式的反射绑定：C# 侧对 Python 值的操作是强类型的，每个内建类型都是一个 C# 类
（`PyStrObject`、`PyIntObject` 等）。公共 API 分为三类：

1. 构造：把 C# 值变成 Python 对象，使用静态工厂。
2. 读取：从 Python 对象取回 C# 值，使用公共属性。
3. 协议操作：调用、属性访问、运算符与协议分发，需要 `PyCallContext`，见文末。

## 对象模型根

所有 Python 对象的公共基类：

```csharp
public partial class PyObject
{
    public PyTypeObject PyType { get; }            // 对象的类型，即 Python 语义的 type()
    public virtual PyTypeObject DefaultPyType { get; }
    public long PyId { get; }                      // 对象身份 id()，稳定长整型
    public override string ToString();             // 调试友好：类型名加 id 加 repr
}
```

`PyType` 返回 `PyTypeObject`，类型对象本身也是 `PyObject`，可读 `FullName` 等信息。`PyId` 是
CPython `id()` 语义的稳定标识。判断 Python 类型可用 C# 的 `is` 模式匹配，
即 `if (obj is PyStrObject s)`，也可走 Python 语义的 `PyType.IsInstance`。

## 构造：C# 到 Python

| Python 类型 | 工厂与单例 |
| --- | --- |
| `NoneType` | `PyNoneObject.None` |
| `bool` | `PyBoolObject.True`、`PyBoolObject.False`、`PyBoolObject.FromBoolean(bool)` |
| `int` | `PyIntObject.FromInteger(int)`、`FromInteger(long)`、`FromInteger(BigInteger)`；常量 `Zero`、`One`、`MinusOne`，小整数有缓存 |
| `float` | `PyFloatObject.FromDouble(double)`；常量 `Zero`、`One`、`MinusOne`、`NaN`、`PositiveInfinity`、`NegativeInfinity`、`NegativeZero`、`Pi`、`E`、`Tau`、`Epsilon` |
| `complex` | `PyComplexObject.FromComplex(System.Numerics.Complex)`、`FromRealImag(double, double = 0)` |
| `str` | `PyStrObject.FromString(string)`、`FromStringNoCache(string)`；`PyStrObject.Empty` |
| `bytes` | `PyBytesObject.FromBytes(byte[])`、`FromBytes(ReadOnlySpan<byte>)`；`PyBytesObject.Empty` |
| `bytearray` | `PyByteArrayObject.FromBytes(ReadOnlySpan<byte>)`、`FromBytes(IEnumerable<byte>)`、`CreateEmpty()` |
| `list` | `PyListObject.CreateList(params IEnumerable<PyObject>)`、`CreateList(params ReadOnlySpan<PyObject>)` |
| `tuple` | `PyTupleObject.CreateTuple(params IEnumerable<PyObject>)`、`CreateTuple(params ReadOnlySpan<PyObject>)`；`PyTupleObject.Empty` |
| `dict` | `PyDictObject.CreateDict()`（空字典）、`CreateDict(context, params IEnumerable<KeyValuePair<PyObject, PyObject>>)`（任意键，需在扩展点内调用，见下文） |
| `set` | `new PySetObject()`、`CreateSet(context, params IEnumerable<PyObject>)`（返回 `PyResult<PySetObject>`）、`CreateSet(params IEnumerable<PyObject>)` |
| `frozenset` | `PyFrozenSetObject.CreateFrozenSet(params IEnumerable<PyObject>)` |
| `range` | `PyRangeObject.CreateRange(BigInteger stop)`、`CreateRange(start, stop, step)` |
| `NotImplemented` | `PyNotImplementedObject.NotImplemented` |
| `Ellipsis` | `PyEllipsisObject.Ellipsis` |

## 读取：Python 到 C#

| Python 类型 | C# 侧访问器 |
| --- | --- |
| `str` | `.Value : string`、`.PyLength : int` |
| `int` | `.Value : BigInteger`（任意精度）；`.IsInt32 : bool`；`.Int32Value : int`，越界取会抛以 `OverflowError` 为内容的 `PyRuntimeException` |
| `float` | `.Value : double` |
| `complex` | `.Value : Complex`、`.Real : double`、`.Imag : double` |
| `bool` | `.BoolValue : bool` |
| `bytes`、`bytearray` | `.this[int] : byte`、`.Length : int` |

典型写法，以异常参数读取为例：

```csharp
if (obj is PyIntObject i && i.IsInt32)
    int n = i.Int32Value;
else if (obj is PyStrObject s)
    string text = s.Value;
```

## 集合类型的 C# 侧操作

部分容器暴露 C# 风格的成员，不经过 Python 协议、无需上下文即可使用：

- `PyListObject`：`this[int]`（可读写）、`Count`、`Add(PyObject)`、`Insert(int, PyObject)`、
  `Clear()`、`CopyTo(PyObject[], int)`、`GetEnumerator()`。
- `PyTupleObject`：`this[int]`（只读）、`Count`、`AsSpan() : ReadOnlySpan<PyObject>`、
  `GetEnumerator()`。
- `PySetObject`：`Add`、`Remove`、`Contains`、`Count`、`Clear`、`CopyTo`，以及集合代数
  `UnionWith`、`IntersectWith`、`ExceptWith`、`SymmetricExceptWith`、`IsSubsetOf`、
  `IsSupersetOf`、`IsProperSubsetOf`、`IsProperSupersetOf`、`Overlaps`、`SetEquals`。
- `PyDictObject`（string 键专用通道）：`this[string]`（可读写，读取未命中抛 `KeyNotFoundException`）、
  `TryGetValue(string, out PyObject?)`、`ContainsKey(string)`、`SetItem(string, PyObject)`、
  `DelItem(string) : bool`、`Count`、`GetEnumerator()`（按插入顺序枚举键值对）。任意 Python 对象
  作键的下标读写删属于协议操作，需在扩展点内使用。

这些成员直接操作底层数据结构，供宿主与扩展代码使用。Python 语义的操作（切片、协议方法、下标越界
行为）应走协议分发。

## 协议操作与 PyCallContext

调用对象（`Call` 与 `CallMethod` 扩展方法）、属性访问（`PyOperators.GetAttr`、`SetAttr`、
`DelAttr`）、运算符（`PyOperators.Add` 等）与协议分发（`PySpecialMethods.Len`、`Iter`、
`GetItem` 等）都是公共 API，但它们的第一个参数是 `PyCallContext`。该类型没有公共构造入口，
上下文由运行时在扩展点中提供：

- 实现 `[PyMethod]` 或 `[PyExport]` 标注的方法时，签名中的 `PyCallContext context` 参数，
  见[用 C# 定义 Python 类型](./custom-types.md)。
- 实现 `PyModuleObject.OnImport(PyCallContext, PyEnvironment)` 时，见
  [用 C# 编写 Python 模块](./custom-modules.md)。

也就是说，在扩展实现内部可以自由使用全部协议操作 API；在扩展之外（纯宿主代码），目前只能使用上文
列出的无上下文 API。这些 API 的清单见 [PyOperators](../api/PyOperators.md) 与
[PySpecialMethods](../api/PySpecialMethods.md)。

## API 参考

- [PyObject](../api/PyObject.md)
- [内建类型速查表](../api/builtin-types.md)
