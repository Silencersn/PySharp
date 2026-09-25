# 运算符与协议分发

源码：`PySharp/Runtime/PyOperators.cs`、`PySpecialMethods.cs`、`PySharp/Runtime/Calls/PyResult.cs`。

Python 的运算符与内建函数不是 C# 运算符重载，而是协议分发：每个类型对象的 `PyTypeObject.Slots`
持有一组协议槽，对应 `__add__`、`__len__`、`__getitem__` 等 dunder 方法；公共入口 `PyOperators`
与 `PySpecialMethods` 负责查槽、处理反射协议与回退规则。这些 API 与扩展点中拿到的 `PyCallContext`
配合使用，见[在 C# 中操作 Python 对象](./python-objects-from-csharp.md)。

## PyResult：错误即值

协议层 API 几乎全部返回 `PyResult` 或 `PyResult<T>`，而不抛异常：

```csharp
public readonly partial struct PyResult
{
    public bool IsSuccessful { get; }       // 无异常
    public bool IsError { get; }            // 有异常
    public PyObject? Value { get; }         // 成功值，default 视为 None
    public PyExceptionObject? Exception { get; }
    public bool IsNotImplemented { get; }   // 值为 NotImplemented
    public bool IsStopIteration { get; }    // 异常为 StopIteration 及其子类
    public bool IsStopAsyncIteration { get; }
    public bool IsAttributeError { get; }
    public bool IsKeyError { get; }         // 缺键判定，容器「查无即错」分支的专用快捷面

    public static PyResult FromValue(PyObject value);
    public static PyResult FromException(PyExceptionObject exception);
    public PyResult<TObject> Of<TObject>() where TObject : PyObject;  // 成功且类型匹配时转强类型
}
```

强类型 `PyResult<T>` 的快捷判定面更小：没有 `IsKeyError` 与 `IsStopAsyncIteration`，需要时先隐式
转 `PyResult` 再判定，详见 [PyResult 参考](../api/PyResult.md)。扩展实现里向 Python 侧发警告用
`context.Warn(...)`，过滤器对其生效，见[警告与数据类](./warnings-and-dataclasses.md)。

- `PyObject` 与 `PyExceptionResult` 都能隐式转换为 `PyResult`，因此实现协议方法时直接
  `return obj;` 或 `return PyResult.TypeError("...");` 即可。由此产生一个陷阱：`return exc;`
  传入裸的 `PyExceptionObject` 会被当作成功值，抛出自定义异常必须包
  `new PyExceptionResult(exc)` 或 `PyResult.FromException(exc)`，见
  [扩展 PySharp](./extending-pysharp.md)。
- `PyResult.TypeError`、`ValueError`、`KeyError`、`IndexError`、`AttributeError`、
  `ZeroDivisionError`、`StopIteration` 等工厂按异常类型命名，由源生成器生成，覆盖全部内建异常类型，
  参数为 `string? format, params ReadOnlySpan<object?> args`。
- `Of<T>()` 在成功但类型不符时抛 `InvalidOperationException`（视为编程错误），错误则原样传递。

把错误变成异常抛出的 `PyUnwrap(context)` 扩展当前为库内部使用。扩展点实现通常直接返回错误结果，
由虚拟机决定传播路径。

## PyOperators：运算符

```csharp
public static class PyOperators
{
    // 二元算术与位运算：Add Sub Mult MatMult TrueDiv FloorDiv Mod Pow LShift RShift BitAnd BitOr BitXor
    public static PyResult Add(PyCallContext context, PyObject left, PyObject right);

    // 比较：Lt LtE Gt GtE Eq NotEq
    public static PyResult Eq(PyCallContext context, PyObject left, PyObject right);

    // 就地（增强赋值）版本：InPlaceAdd 到 InPlaceBitOr
    public static PyResult InPlaceAdd(PyCallContext context, PyObject left, PyObject right);

    // 一元：Invert(~) UAdd(+) USub(-)
    public static PyResult USub(PyCallContext context, PyObject value);

    // 身份与成员
    public static PyBoolObject Is(PyObject left, PyObject right);     // 引用相等
    public static PyBoolObject IsNot(PyObject left, PyObject right);
    public static PyResult<PyBoolObject> In(PyCallContext context, PyObject left, PyObject right);
    public static PyResult<PyBoolObject> NotIn(PyCallContext context, PyObject left, PyObject right);

    // 属性三元组，string 重载会先经过环境的字符串驻留池
    public static PyResult GetAttr(PyCallContext context, PyObject target, string name);
    public static PyResult SetAttr(PyCallContext context, PyObject target, string name, PyObject value);
    public static PyResult DelAttr(PyCallContext context, PyObject target, string name);

    // 逻辑非
    public static PyResult<PyBoolObject> Not(PyCallContext context, PyObject value);
}
```

分发语义与 CPython 对齐的要点：

- 反射协议：类型不同时先试左操作数类型的槽（`__add__`），返回 `NotImplemented` 时再试右操作数的
  反射槽（`__radd__`）；两边都返回 `NotImplemented` 则产生 `TypeError`。
- 子类优先：右操作数的类型是左操作数类型的真子类时，先调用右侧的反射槽。
- 同类型省略反射，对齐 CPython 的 `binary_op1`：两侧类型相同时，算术运算只调用前向槽，自定义
  `__radd__` 只有配合不同类型时才会被 `int + yourtype` 这类表达式触达；比较运算保持双向。
- 一元运算透传：`__neg__`、`__pos__`、`__invert__` 槽存在时结果原样返回，返回 `NotImplemented`
  单例本身也算合法结果；仅槽缺失才报 `TypeError: bad operand type for unary ...`。这一点与二元槽
  「返回 `NotImplemented` 表示放弃」的约定不同。
- `int` 快速路径：两侧均为 `PyIntObject` 时直接走 `PyMath` 的任意精度整数运算。
- 就地运算：先查 `__iadd__` 等就地槽，返回 `NotImplemented` 时回退到普通二元运算。
- 错误消息按运算符族区分：算术用 `unsupported operand type(s) for {op}: '{t1}' and '{t2}'`，
  比较用 `'{op}' not supported between instances of ...`；`**` 显示为 `** or pow()`，就地运算
  显示 `+=` 等增强形式。
- `Eq` 的兜底是身份比较：两侧 `__eq__` 均返回 `NotImplemented` 时退化为引用相等。`NotEq` 先试
  `__ne__`，两侧均返回 `NotImplemented` 时退化为引用不等，不经 `__eq__` 取反。
- `GetAttr` 两段式：先走 `__getattribute__`，含描述符协议与 MRO 查找；仅当结果为 `AttributeError`
  且类型定义了 `__getattr__` 时再调用后者。

## PySpecialMethods：协议方法

对应 Python 的内建函数与协议入口，按类型槽分发，无槽时的回退同样与 CPython 对齐：

| 方法 | 对应协议与内建函数 | 无槽时的行为 |
| --- | --- | --- |
| `Str`、`Repr` | `__str__`、`__repr__`；`str()`、`repr()` | 默认实现，结果必须为 `str`，否则 `TypeError` |
| `Bool` | `__bool__`；`bool()` | 回退 `__len__ > 0`，再回退为 `True` |
| `Len` | `__len__`；`len()` | `TypeError`。bool 结果归一为 0 或 1；负长度报 `ValueError`，超出 `long` 上界报 `OverflowError` |
| `Hash` | `__hash__`；`hash()` | 无槽报 `TypeError`（不可哈希）；结果经 `PyHash.HashLong` 归一化 |
| `Int` | `__int__`；`int()` | 回退 `__index__`，否则 `TypeError` |
| `Index` | `__index__`；下标与切片取整 | `int` 直通，否则 `TypeError` |
| `Float` | `__float__` | `TypeError` |
| `Iter`、`Next` | `__iter__`、`__next__` | 无 `__iter__` 时回退序列协议，有 `__getitem__` 则包装为迭代器 |
| `Await`、`AIter` | `__await__`、`__aiter__` | `TypeError` |
| `GetItem`、`SetItem`、`DelItem` | `__getitem__` 等 | `TypeError`。类型对象特判：`SomeType[...]` 走 `__class_getitem__`，`type[...]` 生成 `types.GenericAlias` |
| `Contains` | `__contains__`；`in` | 回退为迭代逐项相等比较 |
| `Call` | `__call__` | `TypeError`（not callable） |
| `DivMod` | `__divmod__`、`__rdivmod__` | 反射协议后 `TypeError` |
| `Abs`、`Round`、`Trunc`、`Floor`、`Ceil` | `__abs__` 等 | `TypeError` |
| `Format` | `__format__`；`format()` 与 f-string 格式说明符 | 默认按 `str` 处理 |

返回值类型校验（如 `__len__` 必须返回非负 `int`）在分发层统一完成，违反时报 Python 风格的
`TypeError`。

## 调用对象

`PyObject` 的扩展方法位于 `PySharp.Runtime.Calls.Extensions`：

```csharp
public static PyResult Call(this PyObject callable, PyCallContext context,
    IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);
public static PyResult Call(this PyObject callable, PyCallContext context,
    IReadOnlyList<PyObject> args);
public static PyResult Call(this PyObject callable, PyCallContext context);

public static PyResult CallMethod(this PyObject obj, PyCallContext context,
    string methodName, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);
// 以及只传 args 与无参两个简化重载
```

`CallMethod` 等价于先 `GetAttr` 再 `Call`，属性查找失败原样返回错误。

## API 参考

- [PyResult](../api/PyResult.md)
- [PyOperators](../api/PyOperators.md)
- [PySpecialMethods](../api/PySpecialMethods.md)
