# 语义分析

源码：`PySharp/Compilation/AstNodes/SemanticAnalyzer.cs`（816 行）、`SemanticModel.cs`
（350 行），以及 `SemanticAnalyzer.Expr.cs`、`SemanticAnalyzer.Stmt.cs` 两个分部。

Python 没有 C# 意义上的编译期类型检查。PySharp 的语义分析解决的是名字的存储分类：每个变量在每层
作用域里究竟是快局部、单元（cell）、自由变量还是全局或内建。这一分类直接决定 `Emitter` 生成
`LoadFast`、`LoadDeref`、`LoadGlobal`、`LoadName` 中的哪一条。

## 流水线

```csharp
public static SemanticModel Analyze(PyCallContext context, CodeSource source, AstModNode root)
{
    var scope = InternalAnalyze(context, source, root);   // 1 到 4 步
    var model = new SemanticModel(root);
    scope.Bind(model);                                    // 5. 作用域树到节点查找表
    return model;
}
```

`InternalAnalyze` 的四个阶段：

1. `BuildBasicScope`：遍历 AST 构建作用域树（模块、函数、类、推导式），按 `ExprContextType`
   （读取、写入、删除）登记每个名字的首次上下文。
2. `FillUnknownVariables`：把当前作用域无法解释的名字按 Python 规则分类，未赋值即读取的名字
   走全局与内建查找。
3. `CheckClosureAndFillCapturedVariables`：闭包分析。内层函数引用外层局部时，外层变量升格为
   cell（`PyVariableType`），内层登记为自由变量。类作用域的特殊规则（类体变量对方法不可见）
   在此处理。
4. `FillCallableProperties`：为可调用节点补充派生信息，如生成器与异步标记，供函数对象构造使用。

## 数据结构

- `VariableScope` 树（`SemanticModel.cs`）：`OrderedDictionary<string, PyVariableType> Variables`、
  `FirstContext`（名字的首个使用上下文）、`Parent` 与 `Children`。派生类包括
  `RootVariableScope`（模块）及函数、类、推导式等作用域。`QualName` 按父链计算，即嵌套函数的
  限定名。`RootVariableScope.DeclaredGlobals` 记录模块作用域里显式 `global` 声明的名字集合。
- `PyVariableType`（`Compilation/Primitives/`）：变量分类枚举（局部、cell、free、全局、内建等），
  是分析结果的核心载体。
- `SemanticModel`：AST 节点到 `VariableScope` 的绑定表（`AppendScope`、`GetVariableScope<T>`）。
  Emitter 生成变量访问指令时逐节点查询。
- `ScopeStats` 与 `NestedComprehensionStats`：分析器的遍历状态，包括循环深度（用于 `break` 与
  `continue` 的合法性）、`finally` 深度、嵌套推导式栈（推导式作用域与首个迭代器的特判）。

## 根作用域的指令选择

名字分析直接影响 Emitter 在根作用域的指令选择（`Emitter.Expr.EmitName`）：

- 模块代码（非 `onlyAsName`）：根作用域的名字一律发 `LoadGlobal` 与 `StoreGlobal` 族，
  因为模块的「局部」就是全局。
- `eval` 与 `exec` 编译（`onlyAsName = true`，REPL 的「仅为解名字而编译」走同一管线）：名字默认发
  `LoadName` 与 `StoreName` 族，经运行时的 locals mapping 解析，见
  [调用与帧](./calls-and-frames.md)。但 `DeclaredGlobals` 集合里的名字仍发 Global 族，因为显式
  `global x` 声明的写入必须落到真实全局作用域，而不是 exec 或 eval 帧的 locals 映射。这与 CPython
  一致，模块作用域也为 `global` 声明的名字发 `STORE_GLOBAL`。

`DeclaredGlobals` 的登记点在 `SemanticAnalyzer.Stmt` 的 `global` 语句分析处。

## 错误与警告

分析器同样实现 `ICodeMetaInfoProvider`，用遍历路径上的 `_nodesToRoot` 栈提供当前节点区间。
少量编译期即可判定的语法族错误（如作用域非法使用）在此以带定位的 `SyntaxError` 抛出。

分析器还产出编译期语法警告（`SyntaxWarning`，不中断编译）：`is` 与 `is not` 和字面量的比较，
以及 `assert` 测试表达式为非空元组字面量时的
`assertion is always true, perhaps remove parentheses?`。后者对齐 CPython 的 `codegen_assert`，
空元组恒假、变量绑定的元组、括号包裹的非元组都不告警。

## 在整体中的位置

```text
Parser（AST）
    │
    ▼
SemanticAnalyzer（作用域与变量分类）
    │
    ▼
SemanticModel（节点到作用域绑定）
    │
    ▼
Emitter（查表选指令）
```

运行期的对应物是分类结果体现在 `PyCodeObject` 的 `VarNames`（快局部）、`CellVars`（cell）、
`FreeVars`（free）三个名字表上，由帧初始化（`PyInternalFrame.InitArgs` 与 `MakeCell` 指令）
落地为 `PyVariables` 的槽位数组与 `PyCellObject` 单元，见
[调用与帧](./calls-and-frames.md)。
