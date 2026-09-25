# PyResult 参考

源码：`PySharp/Runtime/Calls/PyResult.cs`、`PyResultOfT.cs`。命名空间：`PySharp.Runtime.Calls`。

协议层的统一返回类型，风格为「错误即值」，不抛异常。两个变体：非泛型 `PyResult`
（`readonly partial struct`）与强类型 `PyResult<TObject>`。使用指南见
[运算符与协议分发](../user-guide/operators-and-protocols.md)。

## PyResult

| 成员 | 说明 |
| --- | --- |
| `bool IsSuccessful`、`bool IsError` | 互斥状态，带 `MemberNotNullWhen` 标注以配合空值流分析 |
| `PyObject? Value` | 成功值；`default(PyResult)` 视为 `None`，错误时为 `null` |
| `PyExceptionObject? Exception` | 错误时的 Python 异常对象 |
| `PyExceptionResult ExceptionResult` | 异常的可隐式转换包装；`null` 异常对应 `default` |
| `bool IsNotImplemented` | 值为 `NotImplemented`，即反射协议的「我不处理」信号 |
| `bool IsStopIteration`、`IsStopAsyncIteration`、`IsAttributeError`、`IsKeyError` | 常用异常的快捷判定。`IsKeyError` 多用于容器查找的「缺键非错误」分支，如 dict 视图的 `Contains` |
| `static implicit operator PyResult(PyObject value)` | 值转成功结果 |
| `static implicit operator PyResult(PyExceptionResult value)` | 异常转错误结果；`null` 异常对应 `default` |
| `static PyResult FromValue(PyObject)`、`FromException(PyExceptionObject)` | 显式构造；`null` 参数抛 `ArgumentNullException` |
| `PyResult<TObject> Of<TObject>()` | 成功且值类型匹配时转强类型结果，错误原样传递；类型不匹配抛 `InvalidOperationException`，视为编程错误 |

## PyResult\<TObject\>

成员与非泛型版本大部分对应：`IsSuccessful`、`IsError`、`IsNotImplemented`、`IsStopIteration`、
`IsAttributeError`、`Value : TObject?`、`Exception`、`ExceptionResult`。另有：

| 成员 | 说明 |
| --- | --- |
| `static implicit operator PyResult<TObject>(TObject value)` | 值转成功结果 |
| `static implicit operator PyResult<TObject>(PyExceptionResult value)` | 异常转错误结果 |
| `static implicit operator PyResult(PyResult<TObject> result)` | 强类型转非泛型，错误原样传播 |
| `static FromValue(TObject)`、`FromException(PyExceptionObject)` | 显式构造 |
| `PyResult<TOther> Of<TOther>()` | 同非泛型版本 |

强类型版没有 `IsKeyError` 与 `IsStopAsyncIteration`，这两个判定仅非泛型 `PyResult` 提供。
需要时先经隐式转换 `PyResult r = result;` 再判定。

## 异常工厂

`PyResult` 上有一整套按 Python 异常类型命名的工厂，返回 `PyExceptionResult`，可隐式转为
`PyResult` 或 `PyResult<T>`：

```csharp
return PyResult.TypeError("unsupported operand type(s) for +: '{0}' and '{1}'", a, b);
return PyResult.KeyError(missingKey);
return PyResult.StopIteration();
```

覆盖全部内建异常类型（`TypeError`、`ValueError`、`KeyError`、`IndexError`、`AttributeError`、
`ZeroDivisionError`、`StopIteration`、`SystemExit`、`LookupError`、`ArithmeticError` 等），
签名为 `(string? format, params ReadOnlySpan<object?> args)`，格式串使用 .NET 复合格式
（`{0}` 占位）。`PyResult.KeyError` 与非泛型版的 `IsKeyError` 配套使用，让容器实现区分
「查找失败」与「真正的缺键错误」，见 [dict 系统](../internals/dict-system.md)的 `Contains` 槽。

把 `PyResult` 的错误转为异常抛出的 `PyUnwrap(context)` 扩展方法当前为库内部使用。

## 相关节点

- [PyOperators 参考](./PyOperators.md)、[PySpecialMethods 参考](./PySpecialMethods.md)
