# 数值系统

源码：`PySharp/Modules/Builtins/PyIntObject.cs`、`PyFloatObject.cs`、`PyComplexObject.cs`、
`PySharp/Runtime/PyMath.cs`、`PyHash.cs`、`PySharp/Utility/BigIntegerHelper.cs`。

数值系统有三条主线：任意精度整数（由 `BigInteger` 承载并配小整数缓存）、整数快速路径（`PyMath`
统一实现 int 与 int 的全部运算，由 `PyOperators` 前置调用）、CPython 兼容的哈希归一化
（`PyHash`，模 2⁶¹−1 的 Mersenne 算法）。

## PyIntObject

```csharp
public class PyIntObject : PyObject
{
    internal const int NegativePoolSize = 6;      // -5 到 0
    internal const int PositivesPoolSize = 257;   // 0 到 256
    internal static readonly PyIntObject[] NegativeInts;   // 静态构造期填满
    internal static readonly PyIntObject[] PositiveInts;

    public BigInteger Value { get; }
    public bool IsInt32 => Value >= int.MinValue && Value <= int.MaxValue;
    public int Int32Value => IsInt32 ? (int)Value : throw new PyRuntimeException(PyOverflowErrorObjectType.Shared.Create());

    public static PyIntObject FromInteger(int|long|BigInteger value);   // 命中池或新建
    public static PyIntObject FromIntegerNoCache(BigInteger value);     // 绕过池
    public static PyIntObject Zero { get; } / One / MinusOne;
}
```

- 缓存范围与 CPython 一致，即 `[-5, 256]`。三个 `FromInteger` 重载共享同一套命中逻辑，越界才
  分配。分析器 `PYSP004` 要求库内常量直接使用 `Zero`、`One`、`MinusOne`。
- `Int32Value` 越界时抛 `PyRuntimeException`（内容是 `OverflowError` 的 Python 异常），调用方
  按错误处理，不吞不截断。
- `PyBoolObject : PyIntObject`，即 `bool` 是 `int` 的子类，`True` 与 `False` 为单例。数值路径
  经类继承复用，`isinstance(True, int)` 成立，与 CPython 的类型关系一致。

### 构造与解析

`int(...)` 的 `New` 走 `[PyExport]` 的双重载：`int(number=0)`（字符串经
`BigIntegerHelper.TryParse(..., 10)`，其他对象经 `PySpecialMethods.Int` 协议，含 `__int__` 与
`__index__` 回退）与 `int(string, base=2..36)`。`base=0` 时的前缀自动探测在
`BigIntegerHelper` 内完成。`BigIntegerHelper.ToString(value, numBase)` 与 `ToStringDigits`
反向支撑 `hex()`、`bin()`、`oct()` 与格式化。

整数与字符串互转有位数限制，对齐 CPython 3.11 及以后：`PyIntStrDigitsLimit`（`Utility`，
internal static）持有进程级上限，默认 4300、最小有效值 640、0 禁用，经
`sys.set_int_max_str_digits()` 与 `sys.get_int_max_str_digits()` 暴露。`TryParse` 超限返回
`OverLimit` 状态，调用方按上下文报 `ValueError`（运行期解析）或 `SyntaxError`（源码字面量），
消息与 CPython 同模板。输出侧的 `TryToDecimalString` 在 `str()`、`repr` 与格式化路径做同一检查。
详见[标准库模块覆盖](../python-compat/stdlib-modules.md)。

## PyMath：int 与 int 的快速路径

`PyOperators` 的二元与就地分发在查槽之前检查两侧是否都是 `int`，命中则直接进
`PyMath.CalculatePyIntObject(op, left, right, modulo?)`，见
[协议分发](./protocol-dispatch.md)。未命中则走常规槽分发，而 `PyIntObjectType` 的各二元覆写
（`Add`、`Sub` 等）内部同样先判 `other is PyIntObject` 后转调 `PyMath`，否则回退基类实现。

`CalculatePyIntObject` 对齐 CPython 的要点如下，改动前需要注意：

- `/`（TrueDiv）：先 `BigInteger.DivRem`，整除时直转 `double`（防止 `inf/inf` 得到 `NaN`）；
  非整除时用 `|r|·2⁵³/|right|` 计算 53 位分数再相加，全程无中间 `double` 溢出；
  `|left| >= |right| << 1024` 时精确前置判溢出，报
  `OverflowError: integer division result too large for a float`。
- `//`（FloorDiv）：商为负且有余时减一，即向负无穷取整。
- `%`（Mod）：结果符号跟随除数，两侧符号不同时补上除数。
- 三参 `pow(base, exp, mod)`：负模数在末尾应用（计算全程用 `|mod|`，与 CPython 的 `long_pow`
  一致）；`mod == 1` 得 0；负指数走扩展欧几里得求模逆（`TryModInverse`，非互素报
  `ValueError: base is not invertible...`）；`BigInteger.ModPow` 对负底数返回 C# 的余数语义，
  需归一化到 `[0, modulus)`。
- 移位：负计数报 `ValueError`，计数超出 int 范围报 `OverflowError`。
- 比较族直接比较 `BigInteger`。

## PyHash

`PyHash`（internal static）复刻 CPython `pyhash.h` 的常量：模数 `2⁶¹ - 1`、61 位、long 数字宽
30 位、`±inf` 对应 `±314159`。`PySpecialMethods.Hash` 把槽结果经 `PyHash.HashLong` 归一化后
返回，见[协议分发](./protocol-dispatch.md)。

- `HashLong(BigInteger)`（`long_hash`）：`[-2³⁰, 2³⁰)` 的快速路径直接返回自身；大数按 30 位数字
  从高到低做「循环左移 30 位、加数字、条件减模」的折叠；`-1` 一律映射为 `-2`，因为 CPython 用
  `-1` 作错误哨兵，哈希值必须避开。
- `HashDouble(double, instance)`（`_Py_HashDouble`）：按 `frexp` 式分解为尾数乘 `2ᵉ`，尾数按
  28 位一组折叠，指数对 61 取模后旋转；`+inf` 与 `-inf` 对应 `±314159`；NaN 用对象身份哈希
  （`RuntimeHelpers.GetHashCode(instance)`），因此两个 NaN 对象哈希不同，与 CPython 同义。
- 不变量：整值 float 与 int 哈希一致（`hash(1.0) == hash(1)`，`-1` 与 `-1.0` 都得 `-2`），
  由回归脚本 `test_hash_int_float_regression.py` 专门守护。

## float 与 complex

- `PyFloatObject`（1642 行）：`double Value` 加常量族（`Pi`、`E`、`Tau`、`NaN`、
  `PositiveInfinity`、`NegativeInfinity`、`NegativeZero`、`Epsilon`）。`repr` 的最短往返格式化、
  `round` 与 `%` 语义等边界均有独立回归，如 `test_float_format_regression.py`。
- `PyComplexObject`：包装 `System.Numerics.Complex`，以 `Real` 与 `Imag` 投影。

## BigIntegerHelper

与 Python 语义无关的 `BigInteger` 工具（`Utility`，internal）：`TryParse(ReadOnlySpan<char>,
numBase)` 修剪空白与符号，`base=0` 时自动识别 `0x`、`0b`、`0o` 前缀，否则按十进制，供 `int()`
与编译期字面量解析共用，超位数限制返回 `OverLimit`。`ToString(value, numBase)` 与
`ToStringDigits` 支撑进制输出。

## 相关阅读

[协议分发](./protocol-dispatch.md)（快速路径的调用方）· [对象模型](./object-model.md) ·
[字符串系统](./string-system.md)（同为池加工厂的模式）· [测试体系](./testing.md)
（`test_int_*`、`test_float_*`、`test_hash_*`、`test_pow_*` 回归族）
