# 指令参考：格式化与杂项（Formatting / Intrinsics / Frames / Markers）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs`。`IntrinsicFunctionType` 来自
`Compilation/Bytecodes/IntrinsicFunctionType.cs`。f-string 与 t-string 的跨层链路见
[f-string 与格式化](../fstring-and-format.md)。

通用约定见[常量与名字篇](./instructions-loads.md#通用约定全部指令参考篇适用)。

## f-string 格式化（PEP 498）

格式化指令族的流水线：字面量段 `LoadConst` → 表达式段求值 →（需要时）`ConvertValue` →（需要时）`FormatSimple` / `FormatWithSpec` → `BuildString` 总装。`f'a={a!r:>6}'` 的完整序列见[反汇编走查](./disassembly-walkthroughs.md#6-f-string格式化流水线)，本篇各条目即其零件。

### `ConvertValue` — 转换符应用

- **Arg**：`1 = !s`（`str()`）、`2 = !r`（`repr()`）、`3 = !a`（`ascii()`）；**栈效应**`(value → str_value)`（原地替换）。
- 应用 f-string 的显式转换符。无转换符时**不发射**本指令。

### `FormatSimple` — 无格式说明符的格式化

- **Arg**：无；**栈效应**`(value → str_value)`（原地替换）。
- `format(value, "")`：`__format__` 槽，缺省回退 `str()`（协议回退规则见[协议分发](../protocol-dispatch.md)）。`f'{x}'`（裸插值）用。

### `FormatWithSpec` — 带格式说明符的格式化

- **Arg**：无；**栈效应**`(value, spec → str_value)`。
- 弹出格式说明符字符串，对其下的值调 `PySpecialMethods.Format(value, spec)`。说明符的迷你语言由 `PyFormatSpec` 解析（见[f-string 与格式化](../fstring-and-format.md)）。

### `BuildString` — 字符串总装

- **Arg**：段数 `n`；**栈效应**：`0`：`(→ "")` 压空串；`1`：无操作（**契约**：栈顶必须已是 str，单段无需拼接）；`n ≥ 2`：`(s1 … sn → str)`。
- 把已全部是字符串的段拼接（复用 `CacheBuilder`）。**编译器契约**：进入本指令的每个元素必须已经 `ConvertValue` / `Format*` 布尔化为 str；非 str 属编译器 bug（Debug.Assert）。

## t-string 模板（PEP 750）

### `BuildInterpolation` — 构建插值单元

- **Arg**：位编码。`bit 0`（值 1）：有格式说明符（先弹 spec）；`bit 1-2`（`arg >> 2`）：转换符 `0 = 无`、`1 = s`、`2 = r`、`3 = a`；**栈效应**`(value, expression_str[, spec] → interpolation)`。
- 产出 `PyInterpolationObject`（值 + 表达式文本 + 转换符 + 格式说明符）。栈布局：值在底、表达式源码文本（str）其上、有 spec 时再其上。t-string **不**在构建期求值格式，插值单元原样进入模板（与 f-string 的本质差别）。

### `BuildTemplate` — 组装模板对象

- **Arg**：无；**栈效应**`(strings_tuple, interpolations_tuple → template)`。
- 弹出插值单元元组，与其下的静态文本段元组合成 `PyTemplateObject`。`Template` 对象即"文本段元组 + 插值元组"，渲染发生在消费端（见[f-string 与格式化](../fstring-and-format.md)）。

## 内部函数（Intrinsics）

### `CallIntrinsic1` — 单参内部函数调用

- **Arg**：`IntrinsicFunctionType` 值；**栈效应**`(value → result)`（原地替换）。
- 编译器内部小操作的直通道（避免为琐碎操作创建可调用对象）。**全值表**（`Invalid = 0` 占位不使用）：

| 值 | 名称 | 行为 |
| --- | --- | --- |
| 1 | `ListToTuple` | 列表转元组（字面量元组的两步构建：先 BuildList 再转换） |
| 2 | `_ListToSet` | 列表转集合 |
| 3 | `Print` | REPL 单表达式回显（`DisplayHook`，对齐 CPython `print_expr`）：`None` 原样，否则向 stdout 写 `repr(value)` 并把值绑定到 `builtins._`（上一条结果） |
| 4 | `ImportStar` | `from m import *`：把模块属性批量导入当前帧名字空间 |
| 5 | `TypeVar` | 由名字创建 `TypeVar` 对象（PEP 695 类型参数的运行期形态） |

## 内联帧（推导式）

### `_EnterInlineFrame` — 进入推导式内联帧

- **Arg**：无；**栈效应**：无（帧操作）。
- 以当前帧为模板创建**推导式内联帧**并入帧栈（`CreateInlineFrame`，共享外围的 globals 与栈内存）。推导式的循环体运行在自己的帧形态下，使 `_StoreNameIncludedNonInlineFrame` 等指令能定位"外层非内联帧"（探针实例：`BuildList → _EnterInlineFrame → 循环体 → _ExitInlineFrame`）。帧形态见[调用与帧](../calls-and-frames.md)。

### `_ExitInlineFrame` — 退出内联帧

- **Arg**：无；**栈效应**：无。
- 弹出并释放内联帧（`dispose: true`），回到外层帧继续。推导式产物已在此前压入外层可见的栈位。

## 标记与哨兵

### `NoOperation` — 空操作

- **Arg**：无；**栈效应**：无。
- 字面意义的空操作；正常发射流几乎不产生它（对齐填充用 `__BytecodeEnd`）。

### `__BytecodeEnd` — 尾部填充哨兵

- **Arg**：无；**栈效应**：无。
- 构建器容量尾部的对齐填充；VM 遇到即把指令指针置为流末尾、退出主循环（免去逐条判空）。`Bytecode.TrimExcess` 可裁掉。**只出现在尾部**，不出现在有效代码中间。

### `__LabelFlag` — 待回填标签标记（发射期）

- **Arg**：无（`0b1000_0000`，借用最高位）。
- **不是运行期指令**：发射期跳转占位符的标志位，`Complete()` 回填后即消失，**成品指令流中不存在**。详见[总览 · 指令格式与编码](./README.md#指令格式与编码)。

## 相关阅读

[f-string 与格式化](../fstring-and-format.md)（格式化指令族的跨层链路与 `PyFormatSpec`）· [调用与帧](../calls-and-frames.md)（内联帧形态）· [跳转与容器构造](./instructions-control-flow.md)（推导式的容器指令）
