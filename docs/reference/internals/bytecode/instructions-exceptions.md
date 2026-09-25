# 指令参考：异常与模式匹配（Exceptions / Pattern Matching）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs` 与 `.BytecodeImpl.cs`
（`InternalRaiseVarArgs`、`InternalCheckEgMatch`、`InternalMatchClass`、`InternalMatchKeys`）。
异常组（PEP 654）的算法层见[异常组](../exception-groups.md)。

通用约定见[常量与名字篇](./instructions-loads.md#通用约定全部指令参考篇适用)。

## 状态承载（读前须知）

异常相关指令操作两套栈上状态：**`states.ExceptionHandlers`**（处理器栈，`ExceptionHandler` 记录 finally / except 偏移、进入时栈深、挂起的异常与返回值；状态机的完整说明见[虚拟机](../virtual-machine.md)）与 **`states.Exceptions`**（传播中异常链，`exc_info` 语义）。`try` 语句的发射形态（探针实例）：`_SetupFinally` → `_SetupExcept` → 受保护体 → `_ClearExcept` → `_EnterFinally` → finally 体 → `_ExitFinally`，except 匹配块位于正常流程的 `Jump` 越过区。

## 触发

### `RaiseVarArgs` — raise 语句

- **Arg**：形态数 `0 / 1 / 2`；**栈效应**：`0`：无栈交互；`1`：`(exc →)`；`2`：`(exc, cause →)`。
- `0` = `raise`（重抛当前异常）；`1` = `raise exc`；`2` = `raise exc from cause`。经 `PyCore.Raise` 归化（传类则实例化）后抛出，VM 捕获后转入处理器栈分发。
- **错误**：参数不是异常类 / 实例 → `TypeError`。

### `_CheckExcToRaise` — 延迟异常触发点

- **Arg**：无；**栈效应**：无。
- 生成器 / `yield from` 路径的**延迟抛出**位置：`Send` 家族把待抛异常挂在 `states.ExceptionToRaise` 上，流经本指令时真正抛出（保证异常发生在语句边界而非驱动中途）。无悬挂异常时是空操作。

## except 匹配

### `CheckExcMatch` — 普通 except 条件判定

- **Arg**：无；**栈效应**`(exc_type_or_tuple → bool)`（原地替换）。
- 把栈顶的 except 条件（类型 / 类型元组）编译为判定（`PyCore.MakeExceptCondition`），对当前异常求值，压入布尔。后续 `PopJumpIfFalse` 决定进入或落空（探针实例中与 `_LoadExc` / `StoreGlobal 'e'` / `_PopException` 组成 `except E as e` 的完整序列）。

### `CheckEgMatch` — `except*` 组匹配判定

- **Arg**：匹配消耗完（无剩余）时的跳转目标；**栈效应**`(exc_type_or_tuple → match_group)`。
- 异常组语义的指令端：当前异常不是组则**包装成单元素组**；`SplitExceptionGroup` 按条件二分为 `(rest, match)`；**rest 替换当前异常**（替换语义，见[异常组](../exception-groups.md)），`match` 组压栈供分支体使用。分支体结束后 `_PopExceptionAndJumpIfNull` 检查 rest：为 `None`（全部消耗）则跳往本指令记录的目标，否则继续尝试下一个 `except*`。
- **错误**：条件不合法 → `TypeError`。

### `_CheckMatch` — 模式匹配失败跳转

- **Arg**：失败跳转目标；**栈效应**`(match_result →)`。
- 模式匹配族（下文 `Match*` / `GetLen`）的统一出口检查：栈顶是 `None`（失配哨兵）则弹出并跳转（尝试下一模式 / 落到捕获）；非 `None`（捕获值元组）则保留由后续 `UnpackSequence` 解包绑定。

## 异常栈读取

### `_LoadExc` — 加载当前异常

- **Arg**：无；**栈效应**`(→ exc)`。
- 压入 `states.CurrentException`，即 `except E as e` 中 `e` 的来源。绑定后的隐式清理：块尾先 `e = None` 再 `del e` 两步（CPython 语义），且整块包一层名字清理处理器；`break` / `return` 非局部跳过块尾时由区域展开路径补做（见 `_PopFinally`，见探针实例）。

### `_LoadExcInfo` — 加载 exc_info 三元组

- **Arg**：无；**栈效应**`(→ type, exc, traceback)`（一变三）。
- 压入当前异常的 `(类型, 异常对象, traceback)`，即 `sys.exc_info()` 的指令端形态。

### `_LoadHitExcept` — 查询是否命中过 except

- **Arg**：无；**栈效应**`(→ bool)`。
- 压入"当前处理器是否已命中过 except 分支"，裸 `raise`（重抛）与 finally 语义在指令层需要这个标志。

## 处理器状态机（try 发射形态的骨架）

### `_SetupFinally` — 安装 finally 处理器

- **Arg**：finally 块偏移；**栈效应**：无。
- 压入新 `ExceptionHandler`（记录 `StackDepth` = 进入时栈深）。异常传播时 VM 沿处理器栈找到最近处理器，按偏移进入清理 / 分支代码。

### `_SetupExcept` — 登记 except 偏移

- **Arg**：except 匹配区偏移；**栈效应**：无。
- 给**栈顶**处理器设置 `ExceptOffset`（异常进入点）。与 `_SetupFinally` 配对发射（try 的双段结构）。

### `_ClearExcept` — 撤销 except 登记

- **Arg**：无；**栈效应**：无。
- 清除栈顶处理器的 `ExceptOffset`；受保护体**正常执行完**后、进入 finally 前发射（此后再出异常不再进入本层 except）。

### `_EnterFinally` — 进入 finally 态

- **Arg**：无；**栈效应**：无。
- 把栈顶处理器标记为 Finally 态（finally 体即将执行）。

### `_ExitFinally` — 退出 finally，交接挂起物

- **Arg**：无；**栈效应**：视挂起物而定。
- finally 体结束的收口：处理器标记 End 后弹出；**挂着异常 → 重抛**（穿透语义）；**挂着返回值 → 取出并按进入时栈深清理操作数栈**（从 `finally` 内 `return` 的栈修复，`targetDepth = StackDepth - 1` 的注释即此）。无挂起物则正常继续。
- **错误**：重抛的异常原样传播。

### `_PopException` — 弹出传播链

- **Arg**：无；**栈效应**：无。
- `states.Exceptions.Pop()` 并清栈顶处理器的挂起异常，except 分支体正常结束时使用（异常已处理）。

### `_PopFinally` — 批量弹出处理器记录

- **Arg**：要弹出的记录数 `n`；**栈效应**：无。
- `for (i < arg) states.ExceptionHandlers.Pop()`：`break` / `continue` / `return` **非局部跳出** `with` / `try/finally` / 异步控制流区域时，发射器沿 `EmitterRegion` 区域栈把路径上经过的全部处理器记录一次性弹出（不进入它们的清理代码，清理由发射器内联展开，即 `finally` 体复制、`__exit__` 调用、`except E as name` 的名字删除，见[总览](./README.md#emitter)）。缺少这一步会让迭代器、上下文管理器与异常状态跨块泄漏。

### `_PopExceptionIfTrue` — 条件弹出

- **Arg**：无；**栈效应**`(bool →)`（只读不弹）。
- 栈顶布尔为真 → 执行 `_PopException`（`except*` 分支体的收尾判定之一）。

### `_PopExceptionAndJumpIfNull` — 无剩余则弹出并跳转

- **Arg**：跳转目标；**栈效应**：无。
- 传播链栈顶为 `null`（`except*` 的 rest 已是 `None`，无剩余异常）→ 跳转并弹出，与 `CheckEgMatch` 的 Arg 配对（见上）。

### `_PopMatchException` — `except*` 分支体的传播链弹出

- **Arg**：无；**栈效应**：无。
- `states.Exceptions.Pop()`，`except*` 分支体正常结束时使用。与 `_PopException` 的差别：**只**弹传播链、不清栈顶处理器的挂起异常（组的结算要留到语句尾统一处理，见下两条）。

### `_PrepReraiseStar` — `except*` 结算

- **Arg**：无；**栈效应**：`(orig, res → result_or_None)`。
- `try-except*` 语句尾：弹出 `res`（各分支收集的重抛 / 未捕获项列表，`None` 项表示该分支无产出）与 `orig`（原始组异常），经 `PyCore.PrepReraiseStar` 结算：无任何剩余则压 `None`，否则压合并出的新组（原组作为新组的 `__context__`）。

### `_StarReraise` — 经语句自身处理器重抛

- **Arg**：无；**栈效应**：`(raised →)`。
- 弹出 `_PrepReraiseStar` 的结算结果，挂到**语句自己的处理器记录**的挂起异常位（`Peek().PyException = ...`），让 finally 收口展开时按普通传播路径把它带走重抛，而不是在分支代码里直接 raise。发射于 `except*` 语句的收尾（`Emitter.Stmt.cs` 的 `except*` 展开）。

### `_ExcludeWithResult` — 校准 with 处理器的回滚基准

- **Arg**：下调整数 `n`；**栈效应**：无。
- 把栈顶处理器的 `StackDepth` 减少 `n`：`with` 语句的 `as` 绑定值在受保护体期间驻留操作数栈，异常回滚的基准深度必须把它排除在外，否则展开会在收口代码消费的 `[__exit__, manager]` 对之上留下游离项（发射点：`with` 的 `_SetupExcept` 之后，见[反汇编走查](./disassembly-walkthroughs.md)第 8 篇）。

## 模式匹配（match 语句）

匹配族的统一约定：**subject 保持在栈上不动**，各判定指令在其上追加结果；捕获值以"值元组或 `None`"表达，最终经 `_CheckMatch` 检查、`UnpackSequence` 解包绑定名字。

### `MatchSequence` — 序列模式判定

- **Arg**：无；**栈效应**`(subject → subject, bool)`。
- `PyCore.IsSequenceForMatch`（str / bytes 排除在外的序列判定）压布尔。

### `MatchMapping` — 映射模式判定

- **Arg**：无；**栈效应**`(subject → subject, bool)`。
- `PyCore.IsMappingForMatch` 压布尔。

### `GetLen` — 取长度（长度预检）

- **Arg**：无；**栈效应**`(subject → subject, len)`。
- 对栈顶（peek 不弹）求 `len` 压入，用于序列模式的长度预检（`[a, b]` 至少两项之类）由后续比较跳转完成。
- **错误**：无 `__len__` → `TypeError`。

### `MatchKeys` — 映射键捕获

- **Arg**：无；**栈效应**`(subject, keys_tuple → subject, keys_tuple, values_or_None)`。
- 栈布局 `[subject, keys_tuple]`（键元组在顶）。逐键 `GetItem`：任一键 `KeyError` → 压 `None`（失配）；全部命中 → 压**值元组**。`{"k": k}` 模式的捕获通道。

### `MatchClass` — 类模式捕获

- **Arg**：位置模式数 `n`；**栈效应**`(subject, cls, keys_tuple → subject, cls, keys_tuple, values_or_None)`。
- 栈布局 `[subject, cls, keys_tuple]`。subject 不是 cls 实例 → 压 `None`；否则取 `n` 个**位置**属性（内建特型直接取 subject / `__match_args__` 语义：类的 `__match_args__` 元组按序提供属性名，长度不足 → `TypeError`）与键元组的**关键字**属性（任一 `AttributeError` → 压 `None` 失配），压入值元组。
- **错误**：非类 subject 上调用类模式、`__match_args__` 非元组 / 长度不足 → `TypeError`。

## 相关阅读

[异常组](../exception-groups.md)（split / rest 替换的算法层）· [虚拟机](../virtual-machine.md)（ExceptionHandler 状态机与传播）· [跳转与容器构造](./instructions-control-flow.md)（匹配结果的解包绑定）· [错误处理（用户视角）](../../user-guide/error-handling.md)
