# 指令参考：跳转与容器构造（Control Flow / Builders / Unpacking）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs` 与 `.BytecodeImpl.cs`；发射依据：
`Emitter*.cs`。

通用约定见[常量与名字篇](./instructions-loads.md#通用约定全部指令参考篇适用)。

## 跳转

跳转目标一律是**指令索引**（经 `ExtendedArg` 扩展；成品流中跳转恒定携带 3 条 `ExtendedArg 0` 前缀，由回填策略所致，解读时视作一个逻辑单元，见[总览](./README.md#指令格式与编码)）。

### `Jump` — 无条件跳转

- **Arg**：目标指令索引；**栈效应**：无。
- `nextIndex = arg`，下一条执行目标处指令。循环回边、`if` 汇合点、跳过 `else` 块等。

### `PopJumpIfFalse` — 弹栈为假则跳

- **Arg**：目标索引；**栈效应**`(bool →)`。
- 弹出条件并按布尔值决定是否跳转（假 → 跳）。**编译器契约**：栈顶必须是布尔（与 `UnaryNot` 同）：发射器在条件求值后插入 `ToBool`，已具布尔产生式的会被窥孔省略。
- **错误**：正常路径无（非布尔进入属编译器 bug）。

### `PopJumpIfTrue` — 弹栈为真则跳

- 同 `PopJumpIfFalse`，方向相反（真 → 跳）。`and` / `or` 短路求值与布尔化配合的产物。

### `PopJumpIfNone` — 弹栈为 None 则跳

- **Arg**：目标索引；**栈效应**`(value →)`。
- 按对象身份判定 `is None`（`is PyNoneObject`），**不需要布尔化**，无契约、直接判类型。`x is None` 模式与迭代器哨兵判断用它。

### `ExtendedArg` — 操作数扩展前缀

- **Arg**：高位字节；**栈效应**：无。
- 不是独立语义的指令：主循环在每条指令前做 `instructionArg |= instruction.Arg`，本指令把自己的一节左移 8 位（`instructionArg <<= 8`）留给下一条合成。大端顺序，最多 3 个连用。发射器对 `arg > 255` 的常量索引 / 名字索引 / 大栈深操作数自动生成；跳转目标则**恒定**以 3 前缀编码。

## 容器构造（Build 家族）

构造类指令从栈上取 `n` 个元素组装容器（元素经 `CacheArgs` 缓冲收集，栈自底向上即元素顺序），结果压栈。它们也是 `*` 展开调用的材料组装者（`CallFunctionEx` 前置序列，见[调用与属性下标](./instructions-calls.md)）。

### `BuildList` — 列表字面量

- **Arg**：元素数 `n`；**栈效应**`(e1 … en → list)`。
- 弹 `n` 个元素（经 `CacheArgs` 收集，栈自底向上即元素顺序）组装 `PyListObject` 压栈。**注意**：全部元素为常量的列表通常已在 AST 层折叠进常量池（见[总览](./README.md#编译期常量折叠)），运行期发射的是含非常量元素的场景（推导式另走 `BuildList 0` + `ListAppend` 追加路线）。
- **错误**：无。

### `BuildTuple` — 元组字面量

- **Arg**：元素数 `n`；**栈效应**`(e1 … en → tuple)`。
- 同 `BuildList` 的收集方式，产出不可变的 `PyTupleObject`。两步构建（`BuildList` + `CallIntrinsic1` 的 `ListToTuple`）是另一条等价路线（见[格式化与杂项](./instructions-misc.md#callintrinsic1--单参内部函数调用)）。
- **错误**：无。

### `BuildSet` — 集合字面量

- **Arg**：元素数 `n`；**栈效应**`(e1 … en → set)`。
- 同款收集方式产出 `PySetObject`（构造期即去重）。
- **错误**：元素不可哈希 → `TypeError`。

### `BuildMap` — 字典字面量

- **Arg**：键值对数 `n`（实际弹 `2n` 项）；**栈效应**`(k1, v1 … kn, vn → dict)`。
- 键值按"键在下、值在上"成对入栈；经 `PyDictObject.CreateDict`（逐对走协议 `SetItem`）构建。
- **错误**：键不可哈希 → `TypeError`。

### `BuildSlice` — 切片对象

- **Arg**：`2`（无步长）或 `3`（含步长）；**栈效应**`(start, end[, step] → slice)`。
- 产出 `PySliceObject(start, end, step)`（两参形态 step 为 `None`）。切片是**惰性对象**：`a[1:2]` 的实际切取发生在 `BinarySubscr` 的 `GetItem` 分发里。
- **错误**：无（参数不校验类型，任意对象都可作为切片端点）。

## 追加与合并（推导式 / `*` 展开的搭档）

这族指令的共同形态：**弹出新元素 / 新来源，作用到栈上第 `n` 项的目标容器**（`n` 为目标容器自栈顶向下的深度，由发射器按当时的栈布局计算）。

### `ListAppend` — 追加列表元素

- **Arg**：目标容器深度 `n`；**栈效应**`(value →)`（目标容器不动）。
- 弹值加入 `list(-n)`。推导式的循环体里容器通常在迭代器之下（探针实例：`ListAppend 2`），`*args` 组装序列里紧邻栈底（`ListAppend 1`）。

### `ListExtend` — 列表批量扩展

- **Arg**：目标深度 `n`；**栈效应**`(iterable →)`。
- 弹可迭代对象，`PyExtend` 协议扩展进 `list(-n)`。`f(x, *rest)` 的星参吸收。

### `SetAdd` — 集合追加

- **Arg**：目标深度 `n`；**栈效应**`(value →)`。集合推导式用。
- **错误**：元素不可哈希 → `TypeError`。

### `MapAdd` — 字典追加

- **Arg**：目标深度 `n`；**栈效应**`(key, value →)`（键在下、值在上）。
- 弹值弹键，协议 `SetItem` 进 `dict(-n)`。字典推导式的 `(k: v)` 体。
- **错误**：键不可哈希 → `TypeError`。

### `DictUpdate` — 字典更新（覆盖语义）

- **Arg**：目标深度 `n`；**栈效应**`(map →)`。
- 弹映射 / 可迭代键值对，`Update` 归化后逐对写入 `dict(-n)`，**已存在的键直接覆盖**。`{**a, **b}` 的后者覆盖、`dict.update` 语义。

### `DictMerge` — 字典合并（冲突报错语义）

- **Arg**：目标深度 `n`；**栈效应**`(map →)`。
- 同位置参数见 `DictUpdate`，但**目标已有同名键 → `TypeError`**（"got multiple values for keyword"）。专用于 `f(a=1, **{'a': 2})` 这类必须无冲突的合并场景。这是它与 `DictUpdate` 的唯一差别。

## 解包赋值

### `UnpackSequence` — 定长解包

- **Arg**：目标名字数 `n`；**栈效应**`(iterable → e1 … en)`（一变 n）。
- 弹可迭代对象物化为列表，**长度必须恰为 `n`**，然后**分段逆序压栈**，最终栈顶是最左目标的名字，赋值序列随后从左到右依次 `Store*`（见探针实例与[常量与名字](./instructions-loads.md)）。
- **错误**：长度多 / 少 → `ValueError`（"too many values to unpack" / "not enough"）；不可迭代 → `TypeError`。

### `UnpackEx` — 带星号解包

- **Arg**：位编码。高 16 位 = 星号**前**名字数（pre），低 16 位 = 星号**后**名字数（post）；**栈效应**`(iterable → pre…, star_list, post…)`（一变 pre+post+1）。
- `a, *b, c = …` 的形态：pre 段、`b`（吸收剩余元素的**列表**）、post 段按"赋值序列从左到右依次弹出"的顺序压栈（各段内部逆序，保证最左名字在栈顶）。
- **错误**：元素总数少于 `pre + post` → `ValueError`（星号解包版消息）；不可迭代 → `TypeError`。

## 相关阅读

[调用与属性下标](./instructions-calls.md)（本篇指令的下游消费者）· [运算指令](./instructions-operators.md)（条件布尔化契约）· [总览](./README.md)（跳转编码与字面量折叠）
