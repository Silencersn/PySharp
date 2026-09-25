# 总体架构

PySharp 是编译型的 Python 3 解释器：源码不是逐行树遍历解释，而是先完整编译为自定义字节码，
再由栈式虚拟机执行。整体分三大部分，即编译前端（`Compilation/`）、运行时（`Runtime/`）与
对象及标准库（`Modules/`）。

## 数据流

```text
Python 源码（bytes 或 .py 文件）
    │  PySourceDecoder        字节解码：BOM 与 PEP 263 编码声明
    ▼
CodeSource + CodeText        源码文本与行索引
    │  Lexer                  词法分析
    ▼
TokenSequence
    │  Parser                 语法分析（手写递归下降）
    ▼
AST（ModuleNode 等）
    │  SemanticAnalyzer       作用域与变量分类
    ▼
SemanticModel + VariableScope 树
    │  Emitter                字节码生成
    ▼
Bytecode（指令、常量池、名字池、行表）
    │
    ▼
PyCodeObject
    │  PyCore.Eval → BytecodeVirtualMachine
    ▼
PyObject 世界（slots、MRO、协议分发）
```

编译入口在 `PySharp/Compilation/Compiler.cs`：

```csharp
public static PyResult<PyCodeObject> Compile(string code, CompileMode mode, PyCallContext? context, string? filename = null)
```

三种模式 `CompileMode.Exec`、`Eval`、`Single` 分别对应模块执行、`eval()` 表达式与 REPL 单元。
`InternalCompileSingle` 会在 REPL 场景追加换行以支持块结束判定。执行侧的公共入口是
`PyInterpreter`，见[执行 Python 代码](../user-guide/executing-python.md)。

## 目录职责

| 目录 | 职责 |
| --- | --- |
| `Compilation/` | 编译前端根：`Compiler`（三种 `CompileMode` 入口）、`PySourceDecoder`（PEP 263 字节解码，见[词法分析](./tokenization.md)） |
| `Compilation/Tokenization/` | 词法分析：`Lexer`、`Token`、`TokenSequence`、`TokenType`、正则表 |
| `Compilation/AstNodes/` | 语法分析：`Parser`（按 Expr、Stmt、Match、Mod 分部）、AST 节点、`Reducer`、`SemanticAnalyzer` 与 `SemanticModel` |
| `Compilation/CodeAnalysis/` | 源码文本基础设施：`CodeSource`、`CodeText`（行索引与定位）、`CodeTextSpan`、`CodeMetaInfo` |
| `Compilation/Primitives/` | 编译期枚举：`OperatorType`、`BoolOpType`、`CmpopType`、`ExprContextType`、`UnaryOpType`、`PyVariableType` |
| `Compilation/Bytecodes/` | 字节码层：`OpCode`、`Instruction`、`BytecodeBuilder`、`Emitter`、`LineTable`、`Label`、`IntrinsicFunctionType` |
| `Runtime/` | 执行环境：`PyInterpreter`、`PyCore`、`PyOperators` 与 `PySpecialMethods`、`Calls/`、`Environments/`、`VirtualMachine/`、`IO/`、`Comparison/`、`PyAttributes/` |
| `Modules/Builtins/` | 对象模型：`PyObject`、全部 `Py*Object` 与 `Py*ObjectType`、异常层次、内建函数 |
| `Modules/<其他>/` | 各标准库模块（`Mathematics`、`Sys`、`Threading`、`Queue`、`Typing`、`Random`、`Time`、`Operator`、`Site`、`This`、`String/TemplateLib`、`Dataclasses`、`Warnings`）与 `CSharp/`（用户定义类型的宿主侧布局） |
| `Utility/` | 与 Python 语义无关的 .NET 工具，见[速览](./utility-overview.md)：`ArrayStackHelper`、`BigIntegerHelper`、`ImmutableArrayBuilderPool`、`ConcurrentSet` 等 |
| `Resources/` | 消息资源（`PySR.*`） |
| `Lib/` | 内嵌 Python 源文件（`this.py`、`dataclasses.py`），经 `AdditionalFiles` 供冻结模块生成器使用 |

解决方案中的工具项目（`PySharp.SourceGeneration`、`PySharp.Analyzer` 及各自的 `.Internal`）
在编译期为库生成类型机器并强制代码风格，见[源生成器与代码分析](./source-generators.md)。

## 关键设计决策

- 无 CPython 依赖：词法、语法、语义、字节码与虚拟机全部自研，CPython 源码仅作行为对照。
- AOT 与 trimming 优先：类型机器（方法描述符、slots 注册）由源生成器在编译期产出，运行时零反射，
  库设置 `IsTrimmable` 与 `IsAotCompatible`。
- 错误即值：协议层统一返回 `PyResult`，异常仅在帧边界统一转换与传播为 `PyRuntimeException`，
  避免热路径上的 try 与 catch。
- 值类型化热路径：`Token`、`Instruction`、`PyArguments`、`ValueOperandStack` 等为 struct 或
  ref struct；操作数栈经 `ArrayPool` 租借，以 `Span` 视图操作。
- 对象模型贴近 CPython：类型对象加槽加 MRO 加描述符协议，语义对齐 CPython 3 的数据模型。

## 阅读路线

1. 本篇与[虚拟机](./virtual-machine.md)，了解执行主循环。挂起与恢复机制见
   [生成器与协程系统](./generator-system.md)，异常处理与 `except*` 见
   [异常组](./exception-groups.md)。
2. [词法分析](./tokenization.md)、[语法分析](./parsing.md)、
   [语义分析](./semantic-analysis.md)、[字节码](./bytecode/README.md)，即编译前端顺序。
   f-string、t-string 与格式说明符的跨层链路见[f-string 与格式化](./fstring-and-format.md)。
3. [对象模型](./object-model.md)与[协议分发](./protocol-dispatch.md)，即类型系统。
   用户类创建全流程见[类创建](./class-creation.md)，重点类型见
   [字符串系统](./string-system.md)与[数值系统](./numeric-system.md)。
4. [调用与帧](./calls-and-frames.md)与
   [Environments 与模块解析](./environments-and-modules.md)，即执行环境子系统。
   线程与阻塞原语见 [threading 与 queue](./threading-and-queue.md)。
5. [源生成器与代码分析](./source-generators.md)与[测试体系](./testing.md)，即工具链与质量保障。
   另见 [Utility 速览](./utility-overview.md)与 [contributing](../contributing/build-and-test.md)。
