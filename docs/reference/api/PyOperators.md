# PyOperators 参考

源码：`PySharp/Runtime/PyOperators.cs`。命名空间：`PySharp.Runtime`。

Python 运算符的静态分发入口，`public static class`。所有方法第一个参数为 `PyCallContext`。
语义细节见[运算符与协议分发](../user-guide/operators-and-protocols.md)。

## 二元算术与位运算

`Add`、`Sub`、`Mult`、`MatMult`、`TrueDiv`、`FloorDiv`、`Mod`、`LShift`、`RShift`、`BitAnd`、
`BitOr`、`BitXor`，签名均为：

```csharp
public static PyResult Op(PyCallContext context, PyObject left, PyObject right);
```

幂运算带模数参数：

```csharp
public static PyResult Pow(PyCallContext context, PyObject left, PyObject right, PyObject modulo);
```

分发规则按 CPython 的 `binary_op1` 实现。两侧类型相同时，算术运算只调用前向槽，反射方法不参与
（比较运算保持双向）；右侧类型是左侧类型的真子类时，先调用右侧反射槽再调用左侧槽；其余情况先调
左侧槽，返回 `NotImplemented` 时调右侧反射槽，仍为 `NotImplemented` 则报 `TypeError`。两侧均为
`PyIntObject` 时走 `PyMath` 的任意精度整数快速路径。

## 比较

| 成员 | 说明 |
| --- | --- |
| `Lt`、`LtE`、`Gt`、`GtE` | 反射规则同上（`__lt__` 对应对方的 `__gt__`） |
| `Eq` | 双方 `__eq__` 均为 `NotImplemented` 时退化为引用相等 |
| `NotEq` | 先试 `__ne__`；两侧均返回 `NotImplemented` 时退化为引用不等（`IsNot`），不经 `__eq__` 取反 |

## 身份与成员

| 成员 | 返回 | 说明 |
| --- | --- | --- |
| `Is(PyObject, PyObject)`、`IsNot(...)` | `PyBoolObject` | `ReferenceEquals` 判定，无需上下文 |
| `In(context, left, right)`、`NotIn(...)` | `PyResult<PyBoolObject>` | 调用 `right.__contains__(left)` |

## 就地（增强赋值）运算

`InPlaceAdd`、`InPlaceSub`、`InPlaceMult`、`InPlaceMatMult`、`InPlaceTrueDiv`、`InPlaceFloorDiv`、
`InPlaceMod`、`InPlaceLShift`、`InPlaceRShift`、`InPlaceBitAnd`、`InPlaceBitXor`、`InPlaceBitOr`
为二元形式，`InPlacePow(context, left, right, modulo)` 为三元形式。先查 `__iadd__` 一类的就地槽，
返回 `NotImplemented` 时回退到对应的普通二元运算。

## 属性访问

```csharp
public static PyResult GetAttr(PyCallContext context, PyObject target, string name);
public static PyResult SetAttr(PyCallContext context, PyObject target, string name, PyObject value);
public static PyResult DelAttr(PyCallContext context, PyObject target, string name);
// 另有 name 为 PyObject 的重载；string 版本先经环境 InternPool 驻留
```

`GetAttr` 分两段：先走 `__getattribute__`（槽缺省为类型系统的默认实现，含 MRO 与描述符协议），
结果为 `AttributeError` 且对象定义了 `__getattr__` 时再调用后者。`SetAttr` 与 `DelAttr` 在无槽时
使用类型系统的默认实现，即写入或删除实例 `__dict__`。

## 一元与逻辑

| 成员 | 说明 |
| --- | --- |
| `Invert(context, value)` | `~`，对应 `__invert__` |
| `UAdd(context, value)` | `+`，对应 `__pos__` |
| `USub(context, value)` | `-`，对应 `__neg__` |
| `Not(context, value)` | `not`，经 `Bool` 协议取反 |

一元运算在槽存在时把结果原样透传，槽返回 `NotImplemented` 单例也同样传出（这一点与二元槽的约定
不同）。槽缺失时报 `TypeError`，消息为 `bad operand type for unary <op>`。

## 辅助类型

`PyOperatorTypes` 枚举（`Add` 到 `GtE`）供内部分发使用，同时也对外可见。

## 相关节点

- [PyResult 参考](./PyResult.md)、[PySpecialMethods 参考](./PySpecialMethods.md)
