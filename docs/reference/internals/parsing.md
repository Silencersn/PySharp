# 语法分析

源码：`PySharp/Compilation/AstNodes/`。

语法分析是手写递归下降分析器，不使用解析器生成器，把 `TokenSequence` 组装为强类型 AST。
代码按文件分部组织：

| 文件 | 行数 | 职责 |
| --- | --- | --- |
| `Parser.cs` | 223 | 骨架：三个静态入口、token 游标、关键字与运算符集合、名称改写、字符串池 |
| `Parser.Expr.cs` | 1965 | 表达式产生式：优先级阶梯、推导式、lambda、调用参数、下标与切片 |
| `Parser.Stmt.cs` | 1149 | 简单与复合语句：赋值、控制流、`def` 与 `class`、`with`、`for` 与 `while`、`try`、`import` |
| `Parser.Stmt.Match.cs` | 570 | `match` 与 `case` 模式：序列、映射、类、或模式、捕获 |
| `Parser.Mod.cs` | 51 | 三种模块级形态：`ModuleNode`、`ExpressionNode`、`InteractiveNode` |
| `Parser.Metadata.cs`、`Parser.Utils.cs` | 96 与 39 | 元信息与工具方法 |
| `Reducer.cs` | 28 | AST 后处理，如常量折叠的挂点 |

## 入口与骨架

```csharp
public static ModuleNode ParseModule(PyCallContext, CodeSource, TokenSequence, bool enableNameMangling = true);
public static ExpressionNode ParseExpression(PyCallContext, CodeSource, TokenSequence, bool enableNameMangling = true);      // eval 模式
public static InteractiveNode ParseInteractive(PyCallContext, CodeSource, TokenSequence, bool enableNameMangling = true);     // REPL single 模式
```

分析器内部维护：

- token 游标：`TokenPosition`、`CurrentToken`、`MoveNextToken()`。`NL` 与 `Comment` 一律跳过
  （`SkipUselessToken`）。
- 关键字判定：35 个 Python 关键字的白名单（`IsKeyword`）。关键字在 token 层是 `Name` 类型，
  在此按文本区分。这简化了词法层，也便于处理 `match` 这类软关键字。
- 运算符分组：`AugOperators`（13 个增强赋值）与 `BinaryOperators`（19 个二元）两个 `FrozenSet`，
  由语句与表达式两个分部共用。
- 名称改写（`MangleIdentifier`）：类体内以双下划线开头且不以双下划线结尾的 `__name` 改写为
  `_ClassName__name`，`_classNameTrimmedStack` 跟踪嵌套类。可用 `enableNameMangling: false`
  关闭，供 `exec` 与 `eval` 场景使用。
- 字符串池：标识符文本经静态驻留池与解析器本地池去重，控制分配。
- 前瞻辅助：`IsMatchKeywordsSequence` 无副作用地试探关键字序列，用于消歧，如 `async` 前缀。

## AST 形态

- 三种根节点 `ModuleNode`、`ExpressionNode`、`InteractiveNode` 统一继承 `AstModNode`；
  语句节点为 `AstStmtNode`，表达式节点为 `AstExprNode`。
- 节点携带 `CodeMetaInfo`（源码区间与关键区间），供语义分析与运行期 traceback 使用。`Parser`
  同样实现 `ICodeMetaInfoProvider`，因此语法错误带定位。
- 产生式散布在两个分部中。`Parser.Expr.cs` 按优先级组织，从 `or`、`and`、`not`、比较、`|`
  一路到原子；`Parser.Stmt.cs` 按语句关键字分发。`async` 与 `await`、推导式、海象运算符、
  解包目标、PEP 695 的类型参数、t-string 等语法都有对应节点与解析路径。

## REPL 的容错协作

`PyInterpreter.RunRepl` 捕获 `SyntaxError` 后检查 parser 的当前 token：遇到 `IndentationError`
时先消费多余的 `Dedent`，若已到达 `EndMarker` 则视为「块未写完」并继续读入下一行，否则把错误抛出。
这依赖 parser 暴露的 `CurrentTokenType` 与 `MoveNextToken`，是词法、语法与 REPL 三层的协作点。

## 下游

AST 交给 `SemanticAnalyzer.Analyze`，见[语义分析](./semantic-analysis.md)。`SemanticModel`
连同 AST 一起进入 `Emitter` 生成字节码，见[字节码](./bytecode/README.md)。
