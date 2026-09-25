# PySpecialMethods 参考

源码：`PySharp/Runtime/PySpecialMethods.cs`。命名空间：`PySharp.Runtime`。

Python 协议方法（dunder 分发）的静态入口，`public static class`，对应内建函数与语法层协议。
所有方法第一个参数为 `PyCallContext`，返回 `PyResult` 或强类型 `PyResult<T>`。回退规则与 CPython
对齐，细节见[运算符与协议分发](../user-guide/operators-and-protocols.md)。

## 表示与转换

| 成员 | 签名要点 | 回退与校验 |
| --- | --- | --- |
| `Str`、`Repr` | `(context, obj) → PyResult<PyStrObject>` | 无槽时用默认实现；结果非 `str` 报 `TypeError` |
| `Bool` | `(context, obj) → PyResult<PyBoolObject>` | 依次尝试 `__bool__`、`__len__ > 0`，最后为 `True` |
| `Int` | `(context, obj) → PyResult<PyIntObject>` | 依次尝试 `__int__`、`__index__`，均无则 `TypeError` |
| `Index` | `(context, obj) → PyResult<PyIntObject>` | `int` 直通；否则试 `__index__`，缺失报 `TypeError` |
| `Float` | `(context, obj) → PyResult<PyFloatObject>` | 试 `__float__`，缺失报 `TypeError` |
| `Hash` | `(context, obj) → PyResult<PyIntObject>` | 无槽报不可哈希 `TypeError`；结果经 `PyHash.HashLong` 归一化 |
| `Format` | `(context, obj, formatSpec) → PyResult<PyStrObject>` | `__format__` 缺省按 `str` 处理 |

## 容器与迭代

| 成员 | 说明 |
| --- | --- |
| `Len(context, obj) → PyResult<PyIntObject>` | `__len__`。bool 结果归一为 0 或 1；负长度报 `ValueError`，超出 `long` 上界报 `OverflowError`；无槽报 `TypeError` |
| `Iter(context, obj)` | `__iter__`。无槽时若定义了 `__getitem__` 则按序列协议包装为迭代器，否则报 `TypeError` |
| `Next(context, obj)` | `__next__`。无槽表示 `__iter__` 返回值不是迭代器，报 `TypeError` |
| `GetItem(context, obj, key)` | `__getitem__`。`obj` 为类型对象时特判走 `__class_getitem__`，`type[...]` 直接生成 `GenericAlias` |
| `SetItem(context, obj, key, value)`、`DelItem(context, obj, key)` | 对应槽；无槽报 `TypeError`（不可下标） |
| `Contains(context, obj, item)` | `__contains__`；无槽时回退为迭代逐项相等比较 |

## 异步协议

| 成员 | 说明 |
| --- | --- |
| `Await(context, obj)` | `__await__`；不可等待报 `TypeError` |
| `AIter(context, obj)` | `__aiter__`；缺失报 `TypeError` |

`__anext__` 的逐次推进由异步生成器与事件循环侧按 `Next` 的异步变体处理。

## 数值与调用

| 成员 | 说明 |
| --- | --- |
| `DivMod(context, left, right)` | 依次尝试 `__divmod__`、`__rdivmod__`，均无则 `TypeError` |
| `Abs(context, obj)` | `__abs__`；否则 `TypeError` |
| `Round(context, obj, ndigits)`、`Trunc`、`Floor`、`Ceil` | 对应槽；无槽报 `TypeError` |
| `Call(context, callable, args, kwargs)` | `__call__`；不可调用报 `TypeError` |

## 返回值校验

对约定返回类型的协议（`__len__`、`__str__`、`__bool__`、`__hash__`、`__index__`、`__float__`、
`__format__`、`__int__`），分发层统一校验返回值类型，不符时报：

```text
TypeError: <dunder> returned non-<期望类型> (type <实际类型>)
```

## 相关节点

- [PyOperators 参考](./PyOperators.md)、[PyResult 参考](./PyResult.md)
- [协议分发](../internals/protocol-dispatch.md)
