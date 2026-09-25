# f-string、t-string 与格式说明符

源码：`PySharp/Compilation/Bytecodes/Emitter.Expr.cs`（发射）、
`PySharp/Runtime/VirtualMachine/BytecodeVirtualMachine.cs`（指令族）、
`PySharp/Runtime/PyFormatSpec.cs`（迷你语言解析器）、
`PySharp/Modules/String/TemplateLib/`（t-string 对象）。

字符串插值横跨四层：词法的 f-string 状态机（见[词法分析](./tokenization.md)）产出 token，
语法层组装为 AST 节点，发射层降为格式化指令族，运行期由 `__format__` 协议与格式说明符迷你语言
解析器（`PyFormatSpec`）完成渲染。本篇覆盖后三层。

## AST 节点与发射规则

| 节点 | 发射 |
| --- | --- |
| `JoinedStrNode`（f-string 整体） | 逐段 `LoadExpr` 后执行 `BuildString n`；纯字面量段是常量加载 |
| `FormattedValueNode`（`{expr!conv:spec}`） | 值，可选 `ConvertValue 1/2/3`（`!s`、`!r`、`!a`），有 spec 则加载 spec 后 `FormatWithSpec`，否则 `FormatSimple` |
| `TemplateStrNode` 与 `InterpolationNode`（t-string） | `BuildInterpolation`（位域参数）与 `BuildTemplate` |

```text
f"x={x:>10.3f}"   ⇒   LoadName x → LoadConst ">10.3f" → FormatWithSpec
                      LoadConst "x=" → BuildString 2
```

## 指令族实现

- `ConvertValue arg`：`1` 走 `__str__`，`2` 走 `__repr__`，`3` 走 `ascii()` 内建。`!s`、`!r`、
  `!a` 的转换在格式化之前应用。
- `FormatSimple`：调用 `PySpecialMethods.Format(context, value, "")`。空说明符走 `__format__`
  槽，缺省实现按 `str` 处理，见[协议分发](./protocol-dispatch.md)。
- `FormatWithSpec`：弹出说明符字符串后同样调用 `__format__`。`f"{x:spec}"`、`format(x, "spec")`
  与 `"{}".format` 共用同一分发。
- `BuildString n`：`0` 得到 `Empty`；`1` 断言栈顶已是 `str`，单段无需拼接；`n` 弹出 n 段，经
  `states.CacheBuilder`（复用的 `StringBuilder`）拼接后 `FromString` 入池，见
  [字符串系统](./string-system.md)。
- `BuildInterpolation`（t-string）：参数是位域，`bit0` 表示是否有格式说明符，`bit2` 与 `bit3`
  是转换码（0 为无，1 为 `s`，2 为 `r`，3 为 `a`）。弹出可选的 spec、表达式文本与值，构造
  `PyInterpolationObject`。
- `BuildTemplate`：弹出 interpolations 元组，与栈顶 strings 元组合成 `PyTemplateObject`。

## t-string 运行时对象

t-string 不插值，`t"x={x}"` 产出结构化模板，归属 `string.templatelib` 命名：

- `PyTemplateObject`：`(strings: tuple, interpolations: tuple)` 两个只读属性。静态文本与插值点
  交替排列，`strings` 比 `interpolations` 多一个元素。
- `PyInterpolationObject`：`(value, expression, conversion, format_spec)` 四个属性。`expression`
  是源码文本，`conversion` 为 `None` 或 `"s"`、`"r"`、`"a"`。

渲染由消费方（库或应用）遍历这两个元组完成，解释器只负责结构与安全性分离。

## 格式说明符迷你语言

`PyFormatSpec` 是 `internal readonly struct` 加嵌套的 `ref struct` 解析器，全程使用
`ReadOnlySpan<char>`，零分配：

```text
[[fill]align] [sign] [z] [#] [0] [width] [grouping] [.precision] [grouping] [type]
   < > = ^     + - 空格             数字   , 或 _    数字        , 或 _    见下
```

- 段序固定，任一段不合语法即整体 `TryParse` 失败，调用方报
  `ValueError: Invalid format specifier`。
- `[fill]align` 先按双字符判定（`*<` 中的 `*` 是填充字符），再按单字符。
- `z`（强制正零，PEP 682）、`#`（备用形式与进制前缀）、前导 `0`（零填充感知）是三个独立标志。
- `type` 共 14 种：`b`、`c`、`d`、`e`、`E`、`f`、`F`、`g`、`G`、`n`、`o`、`s`、`x`、`X`、
  `%`。
- 解析结果是 `char?` 与 `int?` 的字段集（`Fill`、`Align`、`Sign`、`CoercePositiveZero`、
  `AlternateForm`、`SignAwareZeroPadding`、`Width`、`WidthGrouping`、`Precision`、
  `PrecisionGrouping`、`Type`），宽度与精度在构造时转为 `int?`。

消费方是 `PyIntObject`、`PyFloatObject` 与 `PyStrObject` 的 `__format__` 覆写。`TryParse`
成功后按 `Type ?? 'd'` 或 `'g'`、`'s'` 分派：

- `int`：进制（`b`、`o`、`x`、`X` 配合 `AlternateForm` 前缀）、符号、分组，以及 `e`、`f`、`g`
  族与 `%` 的精度渲染。
- `float`：符号、分组、`e`、`f`、`g` 族与 `%` 的精度、`z` 旗标与非有限值处理。
- `str`，对齐 CPython 的 `_PyUnicode_FormatAdvancedWriter`：空说明符等价于 `str(obj)`；
  `type` 仅允许 `'s'`，其余报 `Unknown format code`；不允许 sign、`z`、`#` 与 `=` 对齐；
  `precision` 截断前 N 个字符，`width` 以填充字符（默认空格，`SignAwareZeroPadding` 时为 `'0'`）
  按默认 `'<'` 对齐补齐。

其他类型不解析说明符，走 `Format` 分发的默认路径，非空 spec 时按类型报错。给自定义类型添加格式化
支持的正确位置是覆写 `Format` 槽，见[用 C# 定义 Python 类型](../user-guide/custom-types.md)。

## 贡献者提示

- `f"{x}"`、`format(x)` 与 `"%d" % x` 是三条独立管线。前两者共用 `__format__` 分发，`%` 格式化在
  `PyStrObject` 的 `Mod` 覆写内实现。修格式化缺陷时先确认是哪一条。
- `CacheBuilder` 与 `CacheArgs` 是帧间复用缓冲，见
  [生成器与协程系统](./generator-system.md)。新增使用它们的指令时不要跨挂起点持有。
- 回归族包括 `test_fstring.py`、`test_fstring_format.py`、`test_tstring.py`、
  `test_percent_format` 系列回归、`test_float_format_regression.py`、
  `test_int_hexbin_format_regression.py` 与 `test_format_spec_str_regression.py`。

## 相关阅读

[词法分析](./tokenization.md)（f-string 状态机）· [字符串系统](./string-system.md)（驻留与拼接）·
[字节码](./bytecode/README.md)（指令族）· [协议分发](./protocol-dispatch.md)（`Format` 分发与回退）
