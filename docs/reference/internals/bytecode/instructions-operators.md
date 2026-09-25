# 指令参考：运算与栈操作（Operators）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs` 的分发 case；分发目标：
`PyCore.EvalOperator`、`EvalInplaceOperator` 与 `PyOperators`。

本篇覆盖二元 / 比较 / 一元 / 就地运算与布尔化，以及三个纯栈操作指令。运算指令的**分发表就是协议层**：`PyCore.EvalOperator` → `PyOperators` 槽分发（含 int 快速路径与反射协议回退，见[协议分发](../protocol-dispatch.md)与[运算符与协议分发](../../user-guide/operators-and-protocols.md)）。

## 枚举值表（Arg 的语义来源）

三个公共枚举（`Compilation/Primitives/`）的成员顺序即 Arg 数值（已与真实反汇编交叉验证：`BinaryOp 2` = 乘、`CompareOp 4` = `>`）：

**`OperatorType`**（`BinaryOp` / `_AugAssignOp` 使用）

| 值 | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 运算 | `+` | `-` | `*` | `@` | `/` | `%` | `**` |

| 值 | 7 | 8 | 9 | 10 | 11 | 12 |
| --- | --- | --- | --- | --- | --- | --- |
| 运算 | `<<` | `>>` | `\|` | `^` | `&` | `//` |

**`CmpopType`**（`CompareOp` 使用）

| 值 | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 比较 | `==` | `!=` | `<` | `<=` | `>` | `>=` | `is` | `is not` | `in` | `not in` |

> 注意 `CompareOp` 的 Arg 6-9（`is` / `in` 族）通常不用，比较链中的身份与成员判断由专用的 `IsOp` / `ContainsOp` 承担（发射器分流），但枚举与分发路径本身支持它们。

**`UnaryOpType`**（`_UnaryOp` 使用）

| 值 | 0 | 1 | 2 | 3 |
| --- | --- | --- | --- | --- |
| 运算 | `~` | `not` | `+` | `-` |

## 二元与比较运算

### `BinaryOp` — 二元运算

- **Arg**：`OperatorType` 值；**栈效应**`(left, right → result)`。
- 弹出左右操作数，经 `PyCore.EvalOperator(context, op, left, right)` 分发：两侧均为 `int` 时直取 `PyMath` 整数快速路径，否则走二元槽（`Add` / `Sub` / ……）+ 反射协议回退。
- **错误**：类型不支持 → `TypeError`（"unsupported operand type(s)"）；运算自身错误（如除零）→ 对应异常（`ZeroDivisionError` 等）。

### `CompareOp` — 比较运算

- **Arg**：`CmpopType` 值；**栈效应**`(left, right → bool)`。
- 经 `PyCore.EvalOperator(context, cmpop, left, right)` 分发（比较器族 `PyComparer` 的统一入口）。链式比较（`a < b < c`）由发射器展开为"逐段比较 + 短路跳转"，单条指令仍只处理一对操作数。
- **错误**：不可比较 → `TypeError`；`PyComparer` 内部错误传播（如自定义 `__lt__` 抛错）。

### `IsOp` — 身份判断

- **Arg**：`0 = is`，`1 = is not`；**栈效应**`(left, right → bool)`。
- `PyOperators.Is` / `IsNot`：按对象身份（引用 / `PyId` 语义）判断，**不调用任何协议**。
- **错误**：永不报错。

### `ContainsOp` — 成员判断

- **Arg**：`0 = in`，`1 = not in`；**栈效应**`(item, container → bool)`。
- 注意操作数顺序：栈上是 `(item, container)`（item 先入栈）。经 `PyOperators.In` / `NotIn`：优先 `__contains__` 槽，回退迭代协议逐项判等。
- **错误**：容器不可迭代且无 `__contains__` → `TypeError`；判等过程中的错误传播。

## 一元运算

### `_UnaryOp` — 一元运算（分发）

- **Arg**：`UnaryOpType` 值；**栈效应**`(operand → result)`。
- 经 `PyCore.EvalOperator(context, unaryop, operand)` 分发到 `__invert__` / `__neg__` / `__pos__`（及 `not` 的逻辑语义路径）。
- **槽存在时结果原样透传**，对齐 CPython：`__invert__` 等返回 `NotImplemented` 单例本身也是合法结果、原样压栈（与二元槽"返回 NotImplemented 表示放弃"的约定不同）；槽缺失才报 `TypeError: bad operand type for unary ...`。
- **错误**：槽缺失 → `TypeError`。

### `UnaryNot` — 布尔取反

- **Arg**：无；**栈效应**`(bool → bool)`。
- 直接取反栈顶的**布尔值**（按 `PyBoolObject.BoolValue`）。**编译器契约**：栈顶必须已是布尔；`not x` 的完整序列是先把 `x` 经 `ToBool` 布尔化（发射器的窥孔会合并 `ToBool` + `UnaryNot` 的相邻模式，见[总览](./README.md#bytecodebuilder)）。非布尔值进入本指令属于编译器 bug（会以 .NET `InvalidCastException` 崩溃而非 Python 异常）。
- **错误**：正常路径无。

## 就地运算

### `_AugAssignOp` — 增强赋值运算

- **Arg**：`OperatorType` 值；**栈效应**`(left, right → result)`。
- 经 `PyCore.EvalInplaceOperator`：**就地槽优先**（`IAdd` / `ISub` / ……，可变类型原地修改），未命中回退对应二元运算。增强赋值 `x += y` 的求值核心（外层的存储指令由变量分类决定，见[常量与名字](./instructions-loads.md)）。
- **错误**：就地与二元都未命中 → `TypeError`；运算自身错误同 `BinaryOp`。

## 布尔化与栈操作

### `ToBool` — 布尔化

- **Arg**：无；**栈效应**`(value → bool)`（原地替换栈顶）。
- `PySpecialMethods.Bool`：`__bool__` 槽优先，回退 `__len__`（非零为真），再回退"恒真"。条件跳转（`PopJumpIf*`）要求栈顶为布尔，发射器在需要处插入本指令；前值已是布尔产生式（`ToBool` / `IsOp` / `UnaryNot`）时被窥孔省略。
- **错误**：`__bool__` / `__len__` 返回非整数 / 负数等非法值 → `TypeError`。

### `PopTop` — 弹栈丢弃

- **Arg**：无；**栈效应**`(value →)`。
- 丢弃栈顶。表达式语句的求值结果、调用返回值不使用时用它清理。

### `Copy` — 复制栈元素

- **Arg**：深度 `n`（1 = 栈顶自身）；**栈效应**`(…, v_n → …, v_n, v_n)`（复制自顶向下第 `n` 项压栈）。
- 纯栈操作，无协议参与。典型用途：链式比较中保留左操作数、海象运算符的双值消费、赋值目标的多次存储。

### `Swap` — 交换栈元素

- **Arg**：深度 `n`（2 = 交换栈顶与其下 1 项）；**栈效应**`(…, a, b → …, b, a)`（n=2 时）。
- 交换栈顶与自顶向下第 `n` 项。典型用途：`yield from` 的迭代器与值换位、二元求值后的操作数顺序调整。

## 相关阅读

[常量与名字指令](./instructions-loads.md) · [协议分发](../protocol-dispatch.md)（槽与回退规则）· [数值系统](../numeric-system.md)（整数快速路径）· [运算符与协议分发（用户视角）](../../user-guide/operators-and-protocols.md)
