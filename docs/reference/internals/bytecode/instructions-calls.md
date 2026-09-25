# 指令参考：调用与属性下标（Calls / Attrs / Subscripts）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs`（`Call` 族）与 `.BytecodeImpl.cs`；
发射依据：`Emitter.Expr.cs` 的 `EmitCall`。

通用约定（Arg 扩展、栈效应记法、错误冒泡）见[常量与名字篇](./instructions-loads.md#通用约定全部指令参考篇适用)。

## 调用协议总览

三种调用指令（`Call` / `CallKw` / `CallFunctionEx`）只负责**把散布在栈上的调用材料收拾成"可调用 + 位置参数数组 + 关键字参数表"**，然后统一落入内部实现 `__CallImpl`，后者按可调用对象的形态分三条路径（见下文）。参数收集使用帧状态里的复用缓冲 `CacheArgs` / `CacheKwargs`（见[总览](./README.md)与[性能与资源特征](../performance.md)）。

## 调用指令

### `Call` — 位置参数调用

- **Arg**：栈上参数槽个数 `N`（方法调用序列含 self 槽）；**栈效应**`(callable, slot?, args… → result)`。
- 栈布局（自底向上）：`[callable, self_or_null, arg1 … argN]`，即方法调用序列（`LoadMethod` 后）self 槽是对象本身；属性（非方法）路径 self 槽是 `LoadMethod` 压入的 `null`。VM 先探测**最底层参数槽**是否为 `null`（是则实际参数数减一并丢弃该占位），取走参数、弹出可调用后进入 `__CallImpl`。
- **错误**：见 `__CallImpl`。

### `CallKw` — 混合参数调用

- **Arg**：全部实参个数（位置 + 关键字）；**栈效应**`(callable, slot?, args…, kw_values…, names_tuple → result)`。
- 栈布局：位置参数之后是各关键字**值**，栈顶是关键字**名字元组**（发射器以 `LoadConst` 预置，见 `EmitCall`）。VM 先弹名字元组，按 (名字, 值) 建关键字表，其余同 `Call`。
- **错误**：见 `__CallImpl`。

### `CallFunctionEx` — `*args` / `**kwargs` 展开调用

- **Arg**：无；**栈效应**`(callable, args_list, kwargs_dict → result)`。
- `f(*lst, **dct)` 的形态：栈上是**现成的**参数列表与关键字字典（由发射器的 `BuildList` + `ListAppend` / `ListExtend`、`BuildMap` + `DictMerge` 组装，见[跳转与容器构造](./instructions-control-flow.md)）。VM 弹字典逐对收入关键字表（**键必须是 str**，否则 `TypeError`），列表直接作为位置参数数组复用（不拷贝）。
- **错误**：`**` 展开对象的键非字符串 → `TypeError`；其余见 `__CallImpl`。

### `__CallImpl` — 调用公共落点（内部）

- **Arg**：无（仅作为三个调用指令的公共实现体，发射器从不直接发射它）；**栈效应**：由前置指令交接（`callable` / `callArgs` / `callKwargs` 三个局部变量），最终 `(材料 → result)` 压栈。
- 按可调用对象形态分三条路径：
  1. **非 `PyFunctionObject`**（内建函数、类型对象、绑定方法、任何实现 `__call__` 的对象）→ 走协议 `callable.Call(context, args, kwargs)`；
  2. **`PyFunctionObject` 但代码对象标志不属于普通函数形态** → `InternalCall`（特殊代码形态的直接调用路径）；
  3. **普通函数** → `PyArgsDef.TryParse` 按签名校验参数（缺参、多参、未知关键字 → `TypeError`，消息已对齐 CPython 的 `initialize_locals` 语义）→ 创建函数帧（`CreateFuncCallFrame` + `InitArgs` 绑定参数）→ **换帧后 `goto eval_begin` 内联进入被调函数**（`callDepth++`，操作数栈切到新帧）。这是「调用即换帧」的核心机制，返回时经 `eval_end` 回到调用点（见[虚拟机](../virtual-machine.md)与[调用与帧](../calls-and-frames.md)）。
- **错误**：被调方的一切异常原样传播；签名不匹配 `TypeError`。

## 属性访问

### `LoadAttr` — 读取属性

- **Arg**：`name[i]`；**栈效应**`(obj → value)`（原地替换）。
- `PyOperators.GetAttr`：数据描述符 → 实例 `__dict__` → 非数据描述符 / 类属性（MRO）→ `__getattr__` 的两段式编排（见[对象模型](../object-model.md)）。
- **错误**：属性不存在且无 `__getattr__` → `AttributeError`。

### `StoreAttr` — 写入属性

- **Arg**：`name[i]`；**栈效应**`(value, obj →)`。
- 注意栈序：**值先入栈、对象在上**（`obj.attr = value` 的求值顺序）。`PyOperators.SetAttr`：数据描述符 setter 优先，否则写实例 `__dict__`。
- **错误**：只读属性 / 无 setter 且不可写 → `AttributeError`。

### `DeleteAttr` — 删除属性

- **Arg**：`name[i]`；**栈效应**`(obj →)`。
- 弹出对象后 `PyOperators.DelAttr`（描述符 deleter 或从 `__dict__` 移除）。
- **错误**：属性不存在 → `AttributeError`。

## 下标访问

### `BinarySubscr` — 读取下标

- **Arg**：无；**栈效应**`(container, key → value)`。
- `PySpecialMethods.GetItem`（`__getitem__` 槽，含 dict 的 `__missing__` 钩子回退，见[dict 系统](../dict-system.md)）。
- **错误**：序列越界 `IndexError`；dict 缺键 `KeyError`；类型不支持 `TypeError`。

### `StoreSubscr` — 写入下标

- **Arg**：无；**栈效应**`(value, container, key →)`。
- `c[k] = v` 的栈序：**值最底**，其上容器、键（与 `StoreAttr` 同样的"值先入栈"约定）。`PySpecialMethods.SetItem`。
- **错误**：容器不可下标写 `TypeError`（如 tuple）。

### `DeleteSubscr` — 删除下标

- **Arg**：无；**栈效应**`(container, key →)`。
- `del c[k]`；`PySpecialMethods.DelItem`。
- **错误**：缺键 `KeyError` / 越界 `IndexError` / 不支持 `TypeError`。

## 相关阅读

[常量与名字指令](./instructions-loads.md)（`LoadMethod` / `PushNull` 在该篇）· [跳转与容器构造](./instructions-control-flow.md)（`CallFunctionEx` 的参数组装）· [调用与帧](../calls-and-frames.md)（帧创建与参数绑定）· [对象模型](../object-model.md)（属性查找规则）
