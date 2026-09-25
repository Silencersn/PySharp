# 指令参考：常量与名字（Loads / Stores / Deletes）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs` 的分发 case；发射依据：
`Compilation/Bytecodes/Emitter*.cs`。

## 通用约定（全部指令参考篇适用）

- **Arg 扩展**：操作数经 `ExtendedArg` 前缀大端扩展（有效值 = 前缀累积左移 8 位后与 `Arg` 合成）；下文"Arg"均指合成后的有效值。
- **栈效应记法**：`(a, b → c)` 表示弹出 `a`、`b` 后压入 `c`，左端为栈底方向。
- **错误**：VM 内 `throw context.*Error(...)` 与 `PyResult` 的 `.PyUnwrap(context)` 都以 `PyRuntimeException` 冒泡到宿主；指令条目只注明异常类型与触发条件。
- **名字池 / 常量池**：`name[i]` 指 `Bytecode.Names` 池第 `i` 项，`const[i]` 指 `Bytecode.Consts` 池第 `i` 项。
- **指令选择由语义分析决定**：每个名字访问经 `SemanticModel` 分类为 Global / Name / Fast / Deref（及其 Fast 变体），`Emitter.Expr` 按分类走 `AsGlobal()` / `AsName()` / `AsFast(index)` / `AsDeref()`（见[语义分析](../semantic-analysis.md)）。因此模块层代码全部发射 `*Global` 族（模块帧无 locals），函数体内局部名发射 `*Fast` 族。

## 常量与特殊值

### `LoadConst` — 加载常量

- **Arg**：常量池索引 `const[i]`；**栈效应**`(→ value)`。
- 从常量池取对象压栈。常量池在发射期按 Python `==` / `hash` 语义去重；纯字面量表达式已在 AST 层折叠为常量（`1 + 2 * 3` → 单条 `LoadConst 7`，见[总览](./README.md#编译期常量折叠)）。
- **错误**：无（索引由编译器保证合法）。

### `LoadSpecial` — 加载 with 语句特殊方法

- **Arg**：`LoadSpecialMethods` 枚举值：`0 = Enter`（`__enter__`）、`1 = Exit`（`__exit__`）、`2 = AEnter`（`__aenter__`）、`3 = AExit`（`__aexit__`）；**栈效应**`(obj → descriptor)`。
- 取栈顶对象的类型，把对应槽包装为 `PyWrapperDescriptorObject` 压栈（槽不存在则报错）。这是 `with` / `async with` 语句的进入序列之一。
- **错误**：槽缺失时 `TypeError`（消息区分 with / async with）。

### `PushNull` — 压入 null 占位

- **Arg**：无；**栈效应**`(→ null)`。
- 压入一个 C# `null` 槽位。**实际用途**：`_MakeFunctionWithPyArgsDef` 的前置序列中，为**缺失的关键字缺省值**占位，`BuildTuple` 元组的对应槽位保持 `null`，创建函数时被解释为"该参数无缺省"（无需额外哨兵）。另外，调用指令（`Call` / `CallKw`）具备消费栈上 `null` 占位的能力（`LoadMethod` 属性路径压入的 self 槽 `null`），但该 `null` 由 `LoadMethod` 自己产生，与本指令无关。
- **错误**：无。

## 名字加载

### `LoadName` — 按名字语义加载

- **Arg**：`name[i]`；**栈效应**`(→ value)`。
- 走 `PyVariables.LoadName`：先查当前名字空间（locals），未命中回退 globals → builtins。用于语义分类为"Name"的访问（类体、`exec` 名字空间、推导式等无快槽的场景）。
- **错误**：三级作用域都未命中 → `NameError`；槽位存在但未绑定 → `UnboundLocalError`。

### `LoadGlobal` — 加载全局

- **Arg**：`name[i]`；**栈效应**`(→ value)`。
- 走 `LoadGlobal`：globals → `__builtins__` 两级查找，**不查 locals**。模块层代码（含模块层函数名、导入名）全部用它。
- **错误**：`NameError`（含 builtins 未命中时）。

### `LoadFast` — 快局部加载

- **Arg**：局部槽位下标（**不是**名字池索引）；**栈效应**`(→ value)`。
- 直接从帧的 localsPlus span 按下标取值，是零字典查找的快速路径，函数体内编译期已知的局部变量用它。
- **错误**：槽位为 `null`（未绑定，如前向引用的局部名）→ `UnboundLocalError`（消息经 `CodeObject.SlotNameOf` 显示**变量名**，不再是槽位序号）。

### `LoadDeref` — 闭包变量加载（按名）

- **Arg**：`name[i]`；**栈效应**`(→ value)`。
- 走 `LoadDeref`：按名取 cell 并解包。用于语义分类为"Deref"且无槽位下标可用的场景。

### `_LoadDerefFast` — 闭包变量加载（按槽位，内部）

- **Arg**：localsPlus 槽位下标；**栈效应**`(→ value)`。
- 内部快速路径：从槽位直接取 `PyCellObject` 并解包其 `Value`，相当于 `LoadFast` 与 cell 解包的合体，避免按名查表。
- **错误**：槽位为 `null` → `UnboundLocalError`（消息显示变量名）；cell 内容为 `null` 时按 **cellvar / free 区分**：本帧拥有的 cellvar 报 `UnboundLocalError`，引用外层的 free 变量报 `NameError`（对齐 CPython）。

### `LoadMethod` — 加载方法（调用优化路径）

- **Arg**：`name[i]`；**栈效应**`(obj → method_or_attr, self_or_null)`（一换二）。
- 经 `PyCore.GetAttrOrMethod` 取属性：若命中的是**方法**（非托管方法描述符等可绑定对象），栈上留下 `(方法, obj)` 两项供后续 `Call` 直接消费（跳过绑定方法对象的创建）；若是普通属性则留下 `(属性, null)`。`Call` / `CallKw` 以栈上 `null` 识别第二种情形。
- **错误**：属性不存在 → `AttributeError`。

> 异常相关的 `_LoadExcInfo` / `_LoadHitExcept` 属于异常家族篇（见[索引](./README.md#指令集参考)）。

## 名字存储

### `StoreName` — 按名字语义存储

- **Arg**：`name[i]`；**栈效应**`(value →)`。
- 走 `StoreName`：有 locals 的帧存入 locals（快槽或慢字典），无 locals 的帧等价于存 globals。类体中即"类属性赋值"。

### `StoreGlobal` — 存储全局

- **Arg**：`name[i]`；**栈效应**`(value →)`。
- 直接写入 globals 字典；`global` 声明名与模块层赋值用它。

### `StoreFast` — 快局部存储

- **Arg**：局部槽位下标；**栈效应**`(value →)`。
- 按下标写入 localsPlus span，无查找、无错误路径。

### `StoreDeref` — 闭包变量存储（按名）

- **Arg**：`name[i]`；**栈效应**`(value →)`。
- 按名定位 cell 并写入 `Value`。`nonlocal` 声明名的赋值用它。

### `_StoreDerefFast` — 闭包变量存储（按槽位，内部）

- **Arg**：localsPlus 槽位下标；**栈效应**`(value →)`。
- 从槽位取 cell（槽位为 `null` → `UnboundLocalError`），把弹出的值写入 cell。

### `_StoreNameIncludedNonInlineFrame` — 存储并同步外层非内联帧（内部）

- **Arg**：`name[i]`；**栈效应**`(value →)`。
- `StoreName` + 条件同步：若当前帧是**推导式内联帧**（`FrameType.Comprehension`），同时把该名字写入**最近的外层非内联帧**的变量空间。服务推导式中赋值需对外可见的语义（如海象运算符在推导式内绑定外层名字）。

### `_StoreDerefIncludedNonInlineFrame` — cell 存储并同步外层非内联帧（内部）

- **Arg**：`name[i]`；**栈效应**`(value →)`。
- 同上的 Deref 版本：`StoreDeref` + 推导式帧时同步写外层非内联帧的同名 cell。

## 名字删除

### `DeleteName` — 按名字语义删除

- **Arg**：`name[i]`；**栈效应**：无栈交互。
- 走 `DeleteName`：先 locals 后 globals（无 locals 帧等价 `DeleteGlobal`）。`del x` 在类体 / `exec` 中的形态。
- **错误**：未找到 → `NameError`；槽位为 `null` → `UnboundLocalError`。

### `DeleteGlobal` — 删除全局

- **Arg**：`name[i]`；**栈效应**：无。
- 从 globals 字典删除（`PyDictObject` 的 O(n) 删除，见 [dict 系统](../dict-system.md)）。
- **错误**：未命中 → `NameError`。

### `DeleteFast` — 快局部删除

- **Arg**：局部槽位下标；**栈效应**：无。
- 槽位置 `null`（解除绑定，变量回到"未绑定"状态）。
- **错误**：槽位已是 `null` → `UnboundLocalError`。

### `DeleteDeref` — 闭包变量删除

- **Arg**：`name[i]`；**栈效应**：无。
- 把同名 cell 的 `Value` 置 `null`（cell 本身保留，闭包结构不动）。

### `_DeleteDerefFast` — 闭包变量删除（按槽位，内部）

- **Arg**：localsPlus 槽位下标；**栈效应**：无。
- 从槽位取 cell（`null` → `UnboundLocalError`）后置 `Value = null`。

## 相关阅读

[运算指令](./instructions-operators.md) · [语义分析](../semantic-analysis.md)（变量分类的来源）· [调用与帧](../calls-and-frames.md)（localsPlus 布局）

> `LoadAttr` / `StoreAttr` / `DeleteAttr`（属性访问）与 `LoadMethod` 的消费端（`Call` 家族）在[调用与属性下标](./instructions-calls.md)篇。
