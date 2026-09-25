# 指令参考：迭代、生成器与协程（Iteration / Generators / Coroutines）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs`（迭代与挂起区分发）与
`.BytecodeImpl.cs` 的 `InternalSend`。挂起与恢复的完整机制见
[生成器与协程系统](../generator-system.md)。

通用约定见[常量与名字篇](./instructions-loads.md#通用约定全部指令参考篇适用)。

## 迭代基础

### `GetIter` — 取迭代器

- **Arg**：无；**栈效应**`(iterable → iterator)`（原地替换）。
- `PySpecialMethods.Iter`：`__iter__` 槽；序列协议回退由协议层处理（见[协议分发](../protocol-dispatch.md)）。
- **错误**：不可迭代 → `TypeError`。

### `ForIter` — 循环取下一项

- **Arg**：耗尽时的跳转目标（循环出口）；**栈效应**`(iterator → iterator, item)`（成功时压入新项；**耗尽时不压、栈不变**，直接跳转）。
- 对栈顶迭代器调 `PySpecialMethods.Next`：成功压入产出值；`StopIteration` 视为正常耗尽信号跳往出口。`for` 循环体执行完后 `Jump` 回本指令（探针实例：`ForIter 19` / `Jump 6` 的回边结构）。
- **错误**：迭代器内部抛出的**其他**异常原样传播。

### `PopIter` — 丢弃迭代器

- **Arg**：无；**栈效应**`(iterator →)`。
- 循环正常结束后清理栈上残留的迭代器（出口指令，探针实例中紧随 `Jump` 目标之后）。

## 返回

### `ReturnValue` — 函数返回

- **Arg**：返回后额外弹出的栈项数（栈清理量）；**栈效应**`(…, value →)`（弹出返回值后再弹 `n` 项残留）。
- 弹出返回值存入 `returnValue`，并把栈清理回**进入帧时的深度**，残留项典型如 `for` 循环未走完就 `return` 时栈上的迭代器。随后主循环进入 `eval_end` 归返流程（穿透 `finally` 的语义在此处理，见[虚拟机](../virtual-machine.md)）。
- **错误**：无（后续由归返流程决定）。

## 生成器（挂起-恢复）

生成器函数的首条实质指令是 `ReturnGenerator`，之后每次 `yield` 经 `YieldValue` 中途退出主循环。三个关键事实：**挂起点由指令指针回写承载**、**`ReturnGenerator` 把帧与 VM 状态整体搬进生成器对象**、**恢复时从 `eval_begin` 重入**（完整机制见[生成器与协程系统](../generator-system.md)）。

### `ReturnGenerator` — 首次调用即返回生成器

- **Arg**：无；**栈效应**：不经过操作数栈（以"中间值"身份直接结束本轮求值）。
- 函数体执行前的**第一条**指令：按代码对象标志（`AsyncGenerator` / `Coroutine` / 普通生成器）选定三种身份之一的元类型，把当前帧 + VM 状态封存为 `PyBytecodeGeneratorObject` 并作为中间值退出，函数体此时**一条都没执行**。

### `YieldValue` — 产出并挂起

- **Arg**：无；**栈效应**`(value →)`（弹出产出值，同样以中间值身份退出）。
- 弹出产出值、指令指针前移一位（恢复点 = 下一条指令），退出主循环。恢复时该值成为 `next()` / `send()` 的返回值。**`return x` 在生成器里的语义**：返回值进 `StopIteration` 的 args（协议层处理，无独立指令）。

### `GetYieldFromIter` — `yield from` 迭代器归化

- **Arg**：无；**栈效应**`(obj → iterator_or_generator)`（原地替换）。
- 栈顶已是生成器则原样保留（`yield from` 对生成器走委托协议），否则等价 `GetIter`（对普通可迭代对象取迭代器）。之后由 `Send` 驱动。

## 协程与异步

PySharp 的 `await` **没有** .NET `Task` 桥接，协程由 `Send` 指令**同步驱动**（事实详见[生成器与协程系统](../generator-system.md)）。

### `GetAwaitable` — 归化可等待对象

- **Arg**：无；**栈效应**`(obj → awaitable)`（原地替换）。
- 栈顶已是协程则原样保留；否则调 `__await__` 槽取 awaiter。`await x` 的第一步。

### `GetAIter` — 异步迭代器获取

- **Arg**：无；**栈效应**`(async_iterable → aiter)`（原地替换）。
- 调 `__aiter__` 槽；结果**必须具备 `__anext__` 槽**（对齐 CPython 3.14 的校验）。`async for` 的进入指令。
- **错误**：`__aiter__` 结果无 `__anext__` → `TypeError`。

### `GetANext` — 取下一异步步

- **Arg**：无；**栈效应**`(aiter → aiter, awaitable)`（保留 aiter、压入 awaitable）。
- 调 `__anext__` 槽得到下一步对象：非协程则再经 `__await__` 归化。产出的 awaitable 随后由 `Send` 驱动。

### `Send` — 驱动子协程 / 子生成器（`await` / `yield from` 的执行核心）

- **Arg**：完成时的跳转目标；**栈效应**`(iter, value → iter, received_or_result)`（值槽原地更新）。
- 栈布局 `[iter, value]`。三阶段：
  1. **悬挂异常转发**：若状态里挂着待抛异常（`ExceptionToRaise`）：`GeneratorExit` → 调子迭代器 `close()` 后向自身抛出（关闭链）；其他异常 → 优先转发给子迭代器的 `throw()`，无 `throw` 则向自身抛出；
  2. **驱动**：子对象是生成器 → `PySend(value)`（`None` 退化 `PyNext`）；其他迭代器 → `Next` / `send` 方法；
  3. **归位**：`StopIteration` → 以其 `args[0]`（无则 `None`）**替换**值槽并跳往目标（`yield from` 表达式的最终值）；否则产出值留在值槽，继续下一轮 `Send`。
- **错误**：驱动中的一切异常原样传播（这正是 `await` 异常穿透的通道）。

## 相关阅读

[生成器与协程系统](../generator-system.md)（挂起-恢复与三种身份的机制全景）· [虚拟机](../virtual-machine.md)（`eval_end` 归返与换帧）· [跳转与容器构造](./instructions-control-flow.md)（循环的跳转结构）
