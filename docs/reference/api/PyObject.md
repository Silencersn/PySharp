# PyObject 参考

源码：`PySharp/Modules/Builtins/PyObject.cs`。命名空间：`PySharp.Modules.Builtins`。

全部 Python 对象的公共基类，`public partial class`。每个内建类型都直接或间接派生自它。
`PyObjectManagedDict` 为带实例字典（`__dict__`）的对象提供基类。使用指南见
[在 C# 中操作 Python 对象](../user-guide/python-objects-from-csharp.md)。

## 公共成员

| 成员 | 说明 |
| --- | --- |
| `PyTypeObject PyType { get; }` | 对象的类型，即 Python 语义的 `type(obj)`；未显式设置时回退 `DefaultPyType` |
| `virtual PyTypeObject DefaultPyType { get; }` | 类型回退值，每个具体类型覆写为自己的元类型单例，如 `PyStrObjectType.Shared` |
| `long PyId { get; }` | 对象身份，即 `id()` 语义的稳定 `long`，由 `PyAttachedPropertiesManager` 分配 |
| `PyObject()` | 公共构造 |
| `override string ToString()` | 调试友好形式 `类型名{id=..,repr=..}`；repr 经协议求值，失败时退化为 `类型名{id=..}` |

## 派生与扩展

- 子类的 `PyAttributes`（实例 `__dict__`）默认由 `PyAttachedPropertiesManager` 的条件弱表支撑，
  类型为内部接口 `IPyAttributesObject`。不可变类型返回共享的冻结空实现。这些成员为 `internal`，
  扩展作者经协议（`PyOperators.GetAttr` 与 `SetAttr`）操作属性。
- `PyObjectManagedDict`：需要真实每实例字典的类型（模块、异常、用户定义类实例等）的基类。
  `__dict__` 惰性创建为 `PyDictObject`，实现见 [dict 系统](../internals/dict-system.md)。
- 定义新 Python 类型的完整模式（类型对与源生成器特性）见
  [用 C# 定义 Python 类型](../user-guide/custom-types.md)。

## 常用扩展方法

位于 `PySharp.Runtime.Calls.Extensions`：

| 成员 | 说明 |
| --- | --- |
| `PyResult Call(this PyObject, PyCallContext, IReadOnlyList<PyObject>, IReadOnlyDictionary<string, PyObject>)` | 协议调用 `__call__`；另有含 args 与无参两个简化重载 |
| `PyResult CallMethod(this PyObject, PyCallContext, string, IReadOnlyList<PyObject>, IReadOnlyDictionary<string, PyObject>)` | `GetAttr` 加 `Call`；另有两个简化重载 |

这些方法需要 `PyCallContext`，它没有公共构造入口，由扩展点提供。见
[运算符与协议分发](../user-guide/operators-and-protocols.md)。

## 相关节点

- [内建类型速查表](./builtin-types.md)
- [对象模型](../internals/object-model.md)
