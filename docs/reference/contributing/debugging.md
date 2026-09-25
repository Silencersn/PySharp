# 调试指南

调试解释器类项目的核心困难在于问题可能出在四层中的任何一层：词法、语法、语义分析、字节码发射，以及运行期的对象协议。本文给出定位路径与断点建议。

## 先分层：错误属于哪一层

拿到一个故障（通常是某个 `.py` 脚本行为错误或崩溃）后，先按表现粗分：

| 表现 | 大概率层 | 下一步 |
| --- | --- | --- |
| 抛 `SyntaxError` / `IndentationError`，含位置 | 词法或语法 | 位置对不对？token 流是否正确 |
| 编译期崩溃（`Debug.Assert` 或异常发生在 `Emitter` / `SemanticAnalyzer`） | 语义分析或发射 | 断点进对应阶段 |
| 运行期行为错误（值不对、协议未触发、异常类型不对） | VM、协议或类型实现 | 在协议层断点 |
| traceback 行号或文本不对 | `LineTable` 或 `PySR` 消息 | 对照源码区间 |

写一个最小复现脚本（几行内），临时加一个测试方法跑它。`PySharp.Tests` 经 `InternalsVisibleTo("PySharp.Tests")` 可以访问库的 internal 成员，声明在 `PySharp/GlobalUsings.cs`，这是最重要的调试杠杆：

```csharp
[TestMethod]
public void TempDebug()
{
    var module = PyInterpreter.RunFile("test_pyfiles/_repro.py");
    Assert.IsNotNull(module);
}
```

## 断点建议

- **词法**：`Lexer.InternalTokenize` 的状态机主循环，以及 `Token` 的 `Type` / `StringSpan`。缩进类问题看 `_indentationLevels` 栈与 `Indent` / `Dedent` 的产生；f-string 问题看 `_fstringStack` 与 `FStringMiddle` / `FStringDefault` 的状态切换。
- **语法**：`Parser` 各分部对应的产生式方法，按 `Parser.Stmt.cs` / `Parser.Expr.cs` 查；名称改写类问题看 `MangleIdentifier`。
- **语义**：`SemanticAnalyzer.CheckClosureAndFillCapturedVariables` 对应闭包与自由变量分类错误；在 `VariableScope.Variables` 上查某个名字最终落到的 `PyVariableType`。
- **发射**：`Emitter.Emit` 入口加对应语句的发射方法；`BytecodeBuilder.Complete()` 负责标签回填。想看产物可以直接在测试里检查 `PyCodeObject.Bytecode`（internal 可见）的 `Instructions` / `Consts` / `Names` / `StackSize`。
- **VM**：`BytecodeVirtualMachine.Eval` 是一个巨型 `switch`，在具体 OpCode 的 case 上设条件断点（`instruction.OpCode == OpCode.Xxx`）比在循环头断点高效得多；栈内容看 `ValueOperandStack` 的 `_span` / `_size`。异常类问题断 `ExceptionHandler` 状态机。
- **协议与对象**：`PySpecialMethods.*` 与 `PyOperators.*` 的对应方法入口（查槽、回退）；`PyTypeObject.RegisterMethods` 的产物是否正确最终要看生成的代码，见下节。

## 读生成的代码（.g.cs）

手写层看起来对但运行不对时，问题常在“特性标注到生成产物”这一跳：

```
PySharp/obj/Debug/net10.0/generated/
├── PySharp.SourceGeneration.PyTypeGenerator/Py<Name>ObjectType.g.cs      # Shared / FillSlots / RegisterMethods
├── ...PyExceptionGenerator/Py<Name>ObjectType.Exception.g.cs             # Bases 覆写
├── ...PyExportGenerator/<类名>.PyExport.g.cs                             # 函数包装
├── ...PyModuleIncludeGenerator/<类名>.PyModuleInclude.g.cs               # ApplyIncludes
└── PySharp.SourceGeneration.Internal/...                                 # 槽、虚方法、PySpecialNames、异常工厂
```

检查点：方法是否出现在 `RegisterMethods`（`[PyMethod]` 名字拼错就不会出现，且常有 PYARG010 提示）；参数定义数组是否与 `[PyFunctionParameters]` 一致；`FillSlots` 是否调用了预期的 `FillSlot`。生成器诊断（`PYARG*`）本身也是第一道线索，见[源生成器](../internals/source-generators.md)。

## traceback 与错误信息

- `PyRuntimeException.Message` 是格式化好的 Python 风格 traceback（帧、行号、源码行），由 `PyExceptionObject.ToMessage` 结合 `Traceback` 与 `LineTable` 产出。行号错通常是 `LineTableBuilder.Write` 的写入时机问题，关注 `BytecodeBuilder.PushMetaInfo` / `PopMetaInfo`。
- 消息文本错（类型对但措辞或参数错）时查 `PySR` 常量与 `PyResult.<X>Error(format, args)` 的实参顺序，见[错误消息规范](./error-messages.md)。

## REPL 冒烟

`PyInterpreter.RunRepl` 覆盖了独特的交互路径：多行块、空行结束、`InternalCompileSingle` 的 `appendNewLine` 逻辑、`IndentationError` 后的 `Dedent` 消费。改前端后除了测试，手工过一遍 REPL：多行函数定义、未闭合块、`SyntaxError` 后会话不崩。

## `Debug.Assert` 的使用边界

库内大量使用 `Debug.Assert` 表达内部不变量（生成器前提、帧状态、池化前提）。惯例是：

- 只断言“库自身错了才可能违反”的条件，不要用它处理用户输入或 Python 语义错误，那些必须走 `PyResult` 错误或 `SyntaxError` / `TypeError`；
- Release 下 Assert 消失，因此不能依赖其副作用。

## 性能问题的初步定位

先确认是语义错误还是真性能问题。确属性能时，关注点通常是热路径上的分配（协议层是否意外装箱）、`ArrayPool` 归还路径（`OperandStack.Dispose`）、`ExtendedArg` 是否被意外大量产生（发射器窥孔未生效）。修改后用 `test_pyfiles` 全量回归确保语义未变。

## 相关阅读

[总体架构](../internals/architecture.md)、[字节码](../internals/bytecode/README.md)、[虚拟机](../internals/virtual-machine.md)、[测试体系](../internals/testing.md)。
