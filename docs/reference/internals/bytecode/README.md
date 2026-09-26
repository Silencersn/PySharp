# 字节码

源码：`PySharp/Compilation/Bytecodes/`、`PySharp/Compilation/AstNodes/LiteralParser.cs`。

PySharp 定义了自己的字节码格式：每条指令 2 字节（`OpCode : byte` 加 `Arg : byte`），常量与名字
放在独立池中，指令流只引用索引。本篇是格式与发射管线的总览，并索引逐条指令的参考子文档。指令行为
的逐条说明（Arg 语义、栈效应、错误行为）在各家族篇中展开。

## 指令集参考

指令按语义分族，每个操作码都有条目，含下划线前缀的内部码：

| 家族篇 | 覆盖 |
| --- | --- |
| [常量与名字](./instructions-loads.md) | `LoadConst`、`LoadSpecial`、`LoadName`、`LoadGlobal`、`LoadFast`、`LoadDeref`、`_LoadDerefFast`、`StoreName`、`StoreGlobal`、`StoreFast`、`StoreDeref`、`_StoreDerefFast`、`_StoreNameIncludedNonInlineFrame`、`_StoreDerefIncludedNonInlineFrame`、`DeleteName`、`DeleteGlobal`、`DeleteFast`、`DeleteDeref`、`_DeleteDerefFast`、`LoadMethod`、`PushNull` |
| [运算](./instructions-operators.md) | `BinaryOp`、`CompareOp`、`ContainsOp`、`IsOp`、`_UnaryOp`、`UnaryNot`、`ToBool`、`_AugAssignOp`、`Copy`、`Swap`、`PopTop`，附 `OperatorType`、`CmpopType`、`UnaryOpType` 枚举值表 |
| [调用与属性下标](./instructions-calls.md) | `Call`、`CallKw`、`CallFunctionEx`、`__CallImpl`（三条分发路径与调用即换帧）、`LoadAttr`、`StoreAttr`、`DeleteAttr`、`BinarySubscr`、`StoreSubscr`、`DeleteSubscr`，栈序各异，逐条注明 |
| [跳转与容器构造](./instructions-control-flow.md) | `Jump`、`PopJumpIfFalse`、`PopJumpIfTrue`、`PopJumpIfNone`、`ExtendedArg`、`BuildList`、`BuildTuple`、`BuildSet`、`BuildMap`、`BuildSlice`、`ListAppend`、`ListExtend`、`SetAdd`、`MapAdd`、`DictUpdate`、`DictMerge`（覆盖与冲突报错的区别）、`UnpackSequence`、`UnpackEx`（Arg 位编码与压栈顺序） |
| [迭代与生成器协程](./instructions-iteration.md) | `GetIter`、`ForIter`、`PopIter`、`ReturnValue`、`ReturnGenerator`、`YieldValue`、`GetYieldFromIter`、`GetAwaitable`、`GetAIter`、`GetANext`、`Send`（三阶段驱动：悬挂异常转发、驱动、归位） |
| [异常与模式匹配](./instructions-exceptions.md) | `RaiseVarArgs`（0、1、2 参形态）、`CheckExcMatch`、`CheckEgMatch`、`_CheckMatch`、`_LoadExc`、`_CheckExcToRaise`、`_LoadExcInfo`、`_LoadHitExcept`、`_SetupFinally`、`_SetupExcept`、`_ClearExcept`、`_EnterFinally`、`_ExitFinally`、`_PopException`、`_PopExceptionIfTrue`、`_PopExceptionAndJumpIfNull`、`_PopFinally`、`_PopMatchException`、`_PrepReraiseStar`、`_StarReraise`、`_ExcludeWithResult`（`except*` 结算与 with 回滚基准）、`MatchSequence`、`MatchMapping`、`MatchKeys`、`MatchClass`、`GetLen`（try 发射形态骨架与值元组或 `None` 的捕获约定） |
| [函数、类与模块](./instructions-functions.md) | `_MakeFunctionWithPyArgsDef`（前置序列栈布局）、`MakeCell`、`_MakeCellFast`（空壳与原位升级）、`_BuildClass`（含泛型 closure）、`_MakeTypeAlias`、`_SetFunctionTypeParams`、`ImportName`（fromlist 分流）、`ImportFrom`、`SetupAnnotations` |
| [格式化与杂项](./instructions-misc.md) | `ConvertValue`、`FormatSimple`、`FormatWithSpec`、`BuildString`（0、1、n 三分支）、`BuildTemplate`、`BuildInterpolation`（Arg 位编码）、`CallIntrinsic1`（附 `IntrinsicFunctionType` 全值表）、`_EnterInlineFrame`、`_ExitInlineFrame`、`NoOperation`、`__BytecodeEnd`、`__LabelFlag` |
| [反汇编走查](./disassembly-walkthroughs.md) | 八个真实编译产物的逐条注解：常量折叠、分支与循环结构、try 形态骨架、推导式内联帧、f-string 流水线、嵌套代码对象对照、`break` 跳出 `with` 的区域展开 |

八篇家族参考加走查已覆盖 `OpCode.cs` 的全部 118 个操作码，逐个含 Arg 语义、栈效应与错误行为。

## 指令格式与编码

- 2 字节定长：`(OpCode : byte, Arg : byte)`。大数值操作数经 `ExtendedArg` 前缀扩展，采用大端，
  最多 3 个连续前缀，有效值为前缀累积与本指令 `Arg` 合成。
- 跳转目标恒定使用 3 个前缀：标签跳转的回填策略对所有跳转统一编码为 3 条 `ExtendedArg 0` 前缀
  加跳转指令（即使目标偏移小于 256），因为占位符按 4 字节发射、收尾按最大宽度回填。因此真实指令流
  中每个跳转占 4 条指令的位置，解读反汇编输出时把这 4 条视为一个逻辑单元。
- `__LabelFlag = 0b1000_0000`：借用最高位标记「待回填标签」的发射期内部状态，不会出现在成品指令流
  中。
- 尾部填充：构建器容量尾部以 `__BytecodeEnd` 填充，VM 遇到即跳出主循环，避免逐条判空。
  `TrimExcess` 可裁掉。

## 编译期常量折叠

纯字面量表达式在 AST 层（`AstNodes/LiteralParser.cs`）即被折叠：`TryConvertLiteral` 递归识别
字面量及由它们构成的 `BinOp`、`UnaryOp`、`List`、`Tuple`、`Set`、`Dict` 节点，经
`PyCore.EvalOperator`（`NonContextDependency` 上下文）在编译期求值，结果直接进常量池。因此
`x = 1 + 2 * 3` 编译为单条 `LoadConst 7`。这是字面量折叠，不属于发射器的窥孔优化，后者见下文，
只有两条规则。

## 文件一览

| 文件 | 职责 |
| --- | --- |
| `OpCode.cs` | 全部 118 个操作码枚举，下划线前缀为内部专用码 |
| `Instruction.cs` | `readonly struct Instruction`，含 `OpCode` 与 `byte Arg` |
| `BytecodeBuilder.cs` | 发射缓冲：指令列表、标签、常量池与名字池、行表写入，含小窥孔优化 |
| `Emitter.cs`、`.Expr`、`.Stmt` | 按 `SemanticModel` 遍历 AST 生成指令，三个分部为 296、1086、1772 行 |
| `Bytecode.cs` | 成品：不可变指令数组、`LineTable`、`Consts`、`Names` 与预计算的 `StackSize` |
| `LineTable.cs` | 指令索引到源码行的压缩映射（traceback 用），带 `TrimExcess` |
| `Label.cs`、`OpargTypes.cs`、`IntrinsicFunctionType.cs` | 标签、参数类型与 `CallIntrinsic1` 的内部函数表 |

## BytecodeBuilder

- `Emit(OpCode, arg)`：`arg > 255` 时自动前置 `ExtendedArg` 前缀（大端字节序）。
- 标签机制：`DefineLabel()` 与 `MarkLabel(label)`。跳转指令先以 `__LabelFlag` 加标签 ID 的
  4 字节占位发射，`Complete()` 在收尾统一回填为真实指令偏移，同样以最多 3 个 `ExtendedArg`
  编码。
- 池去重：常量经 `PyObjectConstEqualityComparer`（按 Python 的 `==` 与 `hash` 语义）去重入
  `_consts`；名字按序数字符串比较去重入 `_names`。
- 窥孔优化（发射时即做，当前只有两条）：`ToBool` 前的值已是布尔产生式（`ToBool`、`IsOp`、
  `UnaryNot`）则省略；`PopJumpIfFalse` 或 `PopJumpIfTrue` 紧跟 `UnaryNot` 时合并取反。
- 对齐填充：`ToBytecode()` 把构建器容量尾部填 `__BytecodeEnd`，VM 遇到即跳出循环。

## Bytecode 成品

```csharp
public sealed class Bytecode
{
    public ImmutableArray<PyObject> Consts { get; }    // 常量池，含嵌套 PyCodeObject
    public ImmutableArray<string> Names { get; }       // 名字池
    public int StackSize { get; }                      // 静态求得的最大栈深
    public void TrimExcess(bool recursive = false);    // 裁掉尾部填充，可递归处理嵌套 code
}
```

`StackSize` 由工作队列遍历指令流模拟求得，跳转目标入队并记录到达时的栈深，VM 据此一次性分配
操作数栈。`PyCodeObject`（`Name`、`Filename`、`ArgCount`、`VarNames`、`CellVars`、`FreeVars`、
`Bytecode`）是字节码的运行期容器，`PyInterpreter.Execute(PyCodeObject)` 可直接执行。

## Emitter

`Emitter.Emit(context, model, source, onlyAsName)` 的三个分部按语句与表达式遍历 AST：查
`SemanticModel` 决定变量指令类别。复杂语句（`try`、`with`、`for`、推导式、类体、`match`）展开为
「设置、主体、清理」的指令序列，异常路径经标签与 `_SetupFinally` 家族编码。`onlyAsName` 路径服务
于 REPL 中「仅为了解名字而编译」的场景。

控制流区域模型由 `EmitterRegion` 承载，对标 CPython `codegen.c` 的 fblock。发射器维护
`Stack<EmitterRegion>`，每个区域镜像一个运行时需要清理的块：`ForLoop`（驻留迭代器）、
`WhileLoop`（无驻留）、`WithItem` 与 `AsyncWithItem`（`[exit, manager]` 驻留加处理器）、
`TryStatement`、`ExceptHandlerBody`（`except E as name` 的隐式删除记录）、`FinallyBody`、
`AsyncForAwait`、`SavedValue`。`break`、`continue` 与 `return` 做非局部跳出时沿区域栈逐层展开：
弹出处理器记录（`_PopFinally n`），内联复制穿过的 `finally` 体，补上下文管理器的 `__exit__` 调用
与迭代器清理。这是修复「跳出 `with` 或 `try` 时泄漏迭代器、上下文管理器与异常状态」的机制，回归见
`test_with_break_continue.py`、`test_finally_control_flow.py` 与
`test_async_control_flow.py`。

## 相关阅读

[虚拟机](../virtual-machine.md)（指令的消费者）· [语义分析](../semantic-analysis.md)
（变量分类决定指令选择）· [f-string 与格式化](../fstring-and-format.md)（格式化指令族的跨层链路）
