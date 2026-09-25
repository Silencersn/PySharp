# 异常组

源码：`PySharp/Modules/Builtins/PyExceptionObject.Groups.cs`、`PyExceptionObject.cs`、
`PySharp/Runtime/PyCore.cs`（`SplitExceptionGroup`）、
`PySharp/Runtime/VirtualMachine/BytecodeVirtualMachine.cs` 与 `.BytecodeImpl.cs`、
`PySharp/Compilation/Bytecodes/Emitter.Stmt.cs`（`except*` 发射）。

异常组的核心语义是子集匹配：`except* T` 不问「当前异常是不是 T」，而是「把当前异常或异常组里匹配
T 的子集抽出来给我，剩下的留给下一个 `except*`」。本篇按对象模型、split 算法、编译形态与 VM 交接
的顺序走完整条链。

## 对象模型

```text
BaseExceptionGroup (BaseException)
└── ExceptionGroup (BaseExceptionGroup, Exception)   注意双基类
```

- 构造即定型（`PyBaseExceptionGroupObjectType.New` 覆写）：校验 `(message, excs)`，`excs` 必须是
  非空 list 或 tuple 且元素全为异常。随后自动升降级：`BaseExceptionGroup("m", [ValueError()])`
  因子异常全是 `Exception` 而自动成为 `ExceptionGroup`；反之 `ExceptionGroup` 含非 `Exception`
  子异常时报 `TypeError`，并提示改用嵌套的 `BaseExceptionGroup`。自定义子类继承 `ExceptionGroup`
  时同样校验。
- 值类型仍是 `PyExceptionObject`。组身份由 `AsGroup`（`ExceptionGroupInfo?`，含 `Message` 与
  `Exceptions` 列表，`internal`）承载，由 `IsGroup` 判定。`Args` 为 `[message, *excs]`。
  traceback 渲染时按 `ExceptionGroupInfo` 递归输出子异常树。
- `__str__` 为 `"msg (N sub-exceptions)"`。

## derive 与 split

- `derive(excs)`：以原子异常列表派生同型新组，保留原组的 message、traceback、`__cause__` 与
  `__context__`，并按新内容重新定型（全为 `Exception` 则 `ExceptionGroup`，否则
  `BaseExceptionGroup`）。它是 split 的构造原语。
- `split(condition)`：condition 为类型、类型元组或可调用谓词（逐异常调用并做 `bool()` 化）。
  算法是递归二分：遍历子异常，子组递归 split，非空的两半各自作为子组保留并保持嵌套结构；叶子异常
  按谓词分箱；两侧分别 `derive`，空侧为 `None`；返回 `(match, rest)` 二元组。

`except*` 的匹配经 `CallMethod(context, "split", [type])` 走协议分发，因此自定义异常组子类可以
覆写 `split` 定制匹配行为。`PyCore.SplitExceptionGroup` 对返回值做防御性校验：必须是二元组，
元素为异常或 `None`，否则报 `TypeError`。

## 编译形态

每个 `except*` 子句依次发射（`Emitter.Stmt.cs`）：

```text
MarkLabel(exceptor_i):
    <加载匹配类型>
    CheckEgMatch
    _CheckMatch  → next_or_finally     # match 为 None：跳下一个 except* 或 finally
    <绑定名或 PopTop>                   # 绑定的是 match 组
    <子句体>
    DeleteName                          # except* 绑定名在子句末删除
    _PopExceptionAndJumpIfNull → finally   # rest 为 None：异常完全消费，弹栈进 finally
    Jump next
```

普通 `except` 作为对照：`CheckExcMatch` 由 `PyCore.MakeExceptCondition` 构造条件函数后判定当前
异常，不修改正在处理的异常；`except*` 则相反，见下节。

## VM 交接

`InternalCheckEgMatch`（`BytecodeVirtualMachine.BytecodeImpl.cs`）：

```csharp
var exc = states.CurrentException;
if (!exc.IsGroup)
    exc = PyBaseExceptionGroupObjectType.CreateExceptionGroup(string.Empty, [exc]);  // 非组自动包一层
var (rest, match) = PyCore.SplitExceptionGroup(context, exc, stack.Pop());
states.Exceptions.Pop();
states.ExceptionHandlers.Peek().PyException = rest;   // 处理器记录剩余
states.Exceptions.Push(rest!);                        // 当前异常栈顶替换为 rest
stack.Push(match);                                    // match 供 _CheckMatch 判定与绑定
```

三个语义要点：

1. 非组自动包装，因此 `except*` 对普通异常同样工作，即单元素组。
2. 当前异常被替换为 rest。子句体内 `sys.exc_info()`（`_LoadExcInfo`）看到的是剩余部分，而绑定的
   名字是 match 组，与 CPython 一致。
3. 完全消费的出口：rest 为 `None` 时 `_PopExceptionAndJumpIfNull` 弹出异常栈并跳 finally，
   即组被某个 `except*` 全部吃掉后不再重抛。有剩余时按 `ExceptionHandler` 状态机在 try 块结束后
   重新抛出 rest，见[虚拟机](./virtual-machine.md)。

语句尾结算由 `_PrepReraiseStar` 与 `_StarReraise` 完成：`try-except*` 语句结束时，发射器把
「各分支收集的重抛与未捕获项列表加原始组」留在栈上，以 `_PrepReraiseStar` 一次性结算，
`PyCore.PrepReraiseStar` 合并出需要继续传播的新组（无剩余则为 `None`）。`_StarReraise` 把结算
结果挂到语句自身处理器记录的挂起异常位，由 finally 收口按普通传播路径带走重抛。因此分支体内的裸
`raise` 不再绕过语句的收尾结构。三指令的逐条语义见
[异常与模式匹配](./bytecode/instructions-exceptions.md)。

## 贡献者提示

- 改 `except*` 行为时注意双通道：VM 的栈替换逻辑与 `split` 的协议调用（可被子类覆写）。语义缺陷
  可能来自任一侧。
- `ExceptionGroupInfo` 是 `internal` 的承载结构，不是公共 API。
- 回归覆盖在 `test_exception_group.py`（嵌套组、多个 `except*`、derive 与 split、与普通 `except`
  混用），traceback 渲染细节在 `test_exception_details.py`。

## C# 侧边界

组结构的访问器（`IsGroup`、`AsGroup`、`ExceptionGroupInfo`）与构造助手
（`CreateExceptionGroup` 等）全部为 `internal`，因此 C# 宿主代码没有公共的子异常遍历 API。
公共的 `PyExceptionObject.Args` 里可以取到消息与子异常，但形态随构造路径而异：

```text
# 情形一：except* 引擎内部创建（CreateExceptionGroup 拼装）
BaseExceptionGroup("m", [ValueError("a")])
→ Args 约为 [ 'm', ValueError('a') ]          # 扁平

# 情形二：用户在 Python 里直接构造（构造参数原样透传）
BaseExceptionGroup("m", [ValueError("a")])
→ Args 约为 ( 'm', [ValueError('a')] )        # 嵌套
```

因此嵌入侧的稳定策略是在 Python 侧处理组（`except*` 或 `split`），或推动公共遍历 API 的政策变更，
见[路线图](../contributing/roadmap.md)。

## 相关阅读

[虚拟机](./virtual-machine.md)（`ExceptionHandler` 状态机）· [调用与帧](./calls-and-frames.md)
（`states.Exceptions` 栈）· [错误处理](../user-guide/error-handling.md) · [对象模型](./object-model.md)
