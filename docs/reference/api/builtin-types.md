# 内建类型速查表

源码：`PySharp/Modules/Builtins/`。

内建类型位于 `PySharp.Modules.Builtins` 命名空间，组织为「值类型 `Py*Object` 加元类型
`Py*ObjectType`」配对。本表汇总 C# 侧的常用入口。Python 侧的行为以解释器实际实现为准。

## 核心值类型

| Python 类型 | 值类型 | 构造（C# 到 Python） | 读取（Python 到 C#） |
| --- | --- | --- | --- |
| `NoneType` | `PyNoneObject` | `PyNoneObject.None` | 无 |
| `bool` | `PyBoolObject` | `True`、`False`、`FromBoolean(bool)` | `.BoolValue : bool` |
| `int` | `PyIntObject` | `FromInteger(int / long / BigInteger)`、`FromIntegerNoCache(BigInteger)`；`Zero`、`One`、`MinusOne` | `.Value : BigInteger`、`.IsInt32`、`.Int32Value` |
| `float` | `PyFloatObject` | `FromDouble(double)`；`Zero`、`One`、`MinusOne`、`NaN`、`PositiveInfinity`、`NegativeInfinity`、`NegativeZero`、`Pi`、`E`、`Tau`、`Epsilon` | `.Value : double` |
| `complex` | `PyComplexObject` | `FromComplex(Complex)`、`FromRealImag(double, double = 0)`、`FromString(ReadOnlySpan<char>)` | `.Value : Complex`、`.Real`、`.Imag` |
| `str` | `PyStrObject` | `FromString(string)`、`FromStringNoCache(string)`；`Empty` | `.Value : string`、`.PyLength : int` |
| `bytes` | `PyBytesObject` | `FromBytes(byte[])`、`FromBytes(ReadOnlySpan<byte>)`；`Empty` | `this[int] : byte`、`.Length` |
| `bytearray` | `PyByteArrayObject` | `FromBytes(ReadOnlySpan<byte>)`、`FromBytes(IEnumerable<byte>)`、`CreateEmpty()` | `this[int] : byte`（可写） |
| `list` | `PyListObject` | `CreateList(params IEnumerable<PyObject>)`、`CreateList(params ReadOnlySpan<PyObject>)` | `this[int]`（可读写）、`Count`、`Add`、`Insert`、`Clear`、`GetEnumerator()` |
| `tuple` | `PyTupleObject` | `CreateTuple(params IEnumerable<PyObject>)`、`CreateTuple(params ReadOnlySpan<PyObject>)`、`CreateTupleNoCache(ReadOnlySpan<PyObject>)`；`Empty` | `this[int]`、`Count`、`AsSpan()`、`GetEnumerator()` |
| `dict` | `PyDictObject` | `CreateDict()`（空）、`CreateDict(context, params IEnumerable<KeyValuePair<PyObject, PyObject>>)`（任意键，需上下文） | `Count`、`TryGetValue(string, out)`、`this[string]`、`ContainsKey(string)`、`GetEnumerator()`（插入序；见 [dict 系统](../internals/dict-system.md)） |
| `set` | `PySetObject` | `CreateSet(context, params IEnumerable<PyObject>)`（返回 `PyResult<PySetObject>`）、`CreateSet(params IEnumerable<PyObject>)` | `Add`、`Remove`、`Contains`、`Count` 与集合代数（`UnionWith` 等） |
| `frozenset` | `PyFrozenSetObject` | `CreateFrozenSet(params IEnumerable<PyObject>)` | 无 |
| `range` | `PyRangeObject` | `CreateRange(BigInteger stop)`、`CreateRange(BigInteger start, BigInteger stop, BigInteger step)` | 无 |
| `slice` | `PySliceObject` | 由下标语法产生 | 无 |
| `memoryview` | `PyMemoryViewObject` | 经 `memoryview()` 构造 | 无 |
| `Ellipsis` | `PyEllipsisObject` | `PyEllipsisObject.Ellipsis` | 无 |
| `NotImplemented` | `PyNotImplementedObject` | `PyNotImplementedObject.NotImplemented` | 无 |

## 类型与可调用构件

| Python 概念 | C# 类型 |
| --- | --- |
| `type` | `PyTypeObjectType`（`PyTypeObject` 的元类型） |
| 类型对象基类 | `PyTypeObject` 与 `PyTypeObject<T>`（自定义类型继承后者，见[自定义类型](../user-guide/custom-types.md)） |
| `object` 类型 | `PyObjectType` |
| 用户定义类 | `PyObjectManagedDict` 布局的运行时类型 |
| 函数（`def`） | `PyFunctionObject` |
| 内建函数 | `PyBuiltinFunctionOrMethodObject` |
| 绑定方法 | `PyMethodObject`、`PyMethodWrapperObject` |
| `staticmethod`、`classmethod` | `PyStaticMethodObject`、`PyClassMethodObject` |
| `property` | `PyPropertyObject` |
| `super` | `PySuperObject` |
| 描述符 | `PyMemberDescriptorObject`、`PyMethodDescriptorObject`、`PyWrapperDescriptorObject` |

## 执行与迭代构件

| Python 概念 | C# 类型 |
| --- | --- |
| 代码对象（`compile()` 结果，可传 `PyInterpreter.Execute`） | `PyCodeObject`（公开 `Bytecode`、`Name`、`Filename`、`ArgCount`、`VarNames`、`CellVars`、`FreeVars` 等只读属性） |
| 模块 | `PyModuleObject`（公开 `Name`、`Origin`，见[自定义模块](../user-guide/custom-modules.md)） |
| 生成器、协程、异步生成器 | `PyGeneratorObject`、`PyCoroutineObject`、`PyAsyncGeneratorObject` |
| 通用迭代器 | `PyIteratorObject`（序列协议回退时使用）与各容器专用 `Py*IteratorObject` |
| `enumerate`、`filter`、`map`、`zip`、`reversed` | `PyEnumerateObject`、`PyFilterObject`、`PyMapObject`、`PyZipObject`、`PyReversedObject` |
| 闭包单元、帧、traceback | `PyCellObject`、`PyInternalFrame`、`PyTracebackObject` |
| 文件对象（`open()` 返回值） | `PyFileObject` |
| `sys.stdin`、`sys.stdout`、`sys.stderr` | `PyStdIoObject`，环境标准流的包装 |

`PyStdIoObject` 提供 `read([size])`、`readline([size])`、`write(data)`、`flush()`、`close()`、
`readable()`、`writable()` 与属性 `closed`、`name`。用法见
[宿主与 I/O 重定向](../user-guide/hosting.md#编码与标准流对象)。

## 异常类型

`PyExceptionObject` 是全部异常的值类型，公开 `Args`、`Cause`、`Context`、`SuppressContext`、
`Traceback`。每个具体异常类型为 `Py*ErrorObjectType : PyTypeObject<PyExceptionObject>`，层级与
CPython 一致，共 68 个：`PyExceptionObject.Types.cs` 中有 66 个声明，
`PyExceptionObject.Groups.cs` 中手写 `BaseExceptionGroup` 与 `ExceptionGroup` 两个。

警告族也是异常，共 12 个类型，计入上述 68 个：基类 `PyWarningObjectType` 下有 `UserWarning`、
`DeprecationWarning`、`PendingDeprecationWarning`、`RuntimeWarning`、`SyntaxWarning`、
`ImportWarning`、`FutureWarning`、`EncodingWarning`、`UnicodeWarning`、`BytesWarning`、
`ResourceWarning` 共 11 个直接子类，是 `warnings` 模块与自定义警告类别的基座，见
[警告与数据类](../user-guide/warnings-and-dataclasses.md)。

类型判断用 `<Type>.Shared.IsInstance(exc)`。在扩展实现中创建错误用 `PyResult.TypeError(...)`
等工厂，见 [PyResult 参考](./PyResult.md)。定义自定义异常类型用 `[PyException]`，见
[自定义类型](../user-guide/custom-types.md)。

## 相关节点

- [PyObject 参考](./PyObject.md)
- [标准库模块覆盖](../python-compat/stdlib-modules.md)
- [对象模型](../internals/object-model.md)
- [dict 系统](../internals/dict-system.md)
