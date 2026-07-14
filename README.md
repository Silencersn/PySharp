# PySharp

**PySharp** 是一个用 C# 从零实现的 Python 3 解释器，目标平台为 .NET 10（C# 14）。它包含完整的词法分析、语法分析、语义分析、字节码编译和基于栈的虚拟机执行引擎，并提供了丰富的 Python 运行时对象模型与部分内置标准库模块。

## 项目结构

```
PySharp/
├── PySharp/                              # 核心运行时库
│   ├── Compilation/                      # 编译前端
│   │   ├── Tokenization/                 # 词法分析（Lexer）
│   │   ├── AstNodes/                     # 抽象语法树（Parser + Reducer + SemanticAnalyzer）
│   │   ├── CodeAnalysis/                 # 源代码位置追踪
│   │   ├── Bytecodes/                    # 字节码编译器与发射器（Compiler + Emitter）
│   │   └── Primitives/                   # 基础类型定义（运算符、比较等枚举）
│   ├── Runtime/                          # 运行时
│   │   ├── Calls/                        # 调用约定与参数处理（PyCallContext、PyResult、PyArguments）
│   │   ├── Environments/                 # 运行环境（模块、路径、导入系统）
│   │   ├── IO/                           # 虚拟文件系统（MemoryFileSystem、PhysicalFileSystem）
│   │   ├── PyAttributes/                 # Python 类型集成特性标注（[PyType]、[PyMethod] 等）
│   │   ├── VirtualMachine/               # 字节码虚拟机执行引擎
│   │   └── Comparison/                   # 对象比较器
│   ├── Modules/                          # 内置 Python 模块
│   │   ├── Builtins/                     # 内置类型与函数（~80 个文件）
│   │   ├── Mathematics/                  # math 模块
│   │   ├── Time/                         # time 模块
│   │   ├── Random/                       # random 模块
│   │   ├── Threading/                    # threading 模块
│   │   ├── Queue/                        # queue 模块
│   │   ├── Operator/                     # operator 模块
│   │   ├── Site/                         # site 模块
│   │   ├── This/                         # this 模块
│   │   ├── String/                       # f-string 模板运行时基础设施
│   │   ├── Typing/                       # `type` 别名语句运行时支持
│   │   └── CSharp/                       # C# 互操作运行时支持
│   ├── Lib/                              # Python 标准库源码（.py 文件，编译为冻结模块）
│   │   └── this.py                       # Python 之禅（罗特13加密）
│   ├── Utility/                          # 工具类（StringKeyDict、BigIntegerHelper、SpanExtensions 等）
│   └── Resources/                        # 字符串资源文件（PySR.*.cs）
├── PySharp.SourceGeneration/             # 公共 Roslyn 源码生成器
├── PySharp.SourceGeneration.Internal/    # 内部 Roslyn 源码生成器
├── PySharp.Analyzer/                     # 公共 Roslyn 分析器（PYSP001、PYSP002）
├── PySharp.Analyzer.Internal/            # 内部 Roslyn 分析器（PYSPI001–PYSPI008）
├── PySharp.Tests/                        # 单元测试（MSTest，含大量 Python 测试脚本）
├── PySharp.sln                           # 解决方案文件
├── global.json                           # .NET SDK 版本配置
├── .editorconfig                         # 编辑器配置
├── .gitattributes                        # Git 属性配置
└── .gitignore                            # Git 忽略规则
```

## 功能特性

### 编译前端

- **词法分析** — `Lexer` 基于正则表达式实现完整的 Python 3 词法扫描，支持关键字、标识符、数字字面量（整数、浮点数、复数、进制前缀）、字符串（含 f-string、字节串）、缩进/取消缩进、注释等所有标准 token 类型。使用预编译正则表达式提升性能。
- **语法分析** — `Parser` 为递归下降解析器，直接生成 AST。支持 Python 3 的全部语法结构，包括模式匹配（`match`/`case`，含序列、映射、类模式）。另提供 `Reducer` 用于常量折叠优化。
- **语义分析** — `SemanticAnalyzer` 进行作用域解析、变量绑定、名称查找表构建，生成 `SemanticModel`。
- **字节码编译** — `Emitter` 将语义模型编译为自定义字节码指令序列。`Compiler` 提供三种编译模式：`CompileExec`（模块）、`CompileEval`（表达式）、`CompileSingle`（交互式）。支持 `CodeSource`/`CodeText` 精确源码位置追踪。

### 字节码虚拟机

基于栈的虚拟机（`BytecodeVirtualMachine`），执行由编译器生成的字节码。支持 90+ 条指令，涵盖：

- 变量加载/存储（局部 `LoadFast`、全局 `LoadGlobal`、闭包 `LoadDeref`）
- 属性访问（`LoadAttr`、`StoreAttr`、`DeleteAttr`）
- 算术与比较运算（`BinaryOp`、`CompareOp`、`ContainsOp`、`IsOp`）
- 控制流（条件跳转 `PopJumpIfFalse/True`、循环 `ForIter`）
- 函数调用与参数传递（`Call`、`CallKw`、`CallFunctionEx`）
- 异常处理（`RaiseVarArgs`、`CheckExcMatch`、`_SetupFinally`/`_SetupExcept`/`_EnterFinally`/`_ExitFinally`/`_PopException`）
- 生成器与协程（`YieldValue`、`GetYieldFromIter`、`GetAwaitable`、`Send`）
- 异步迭代器（`GetAIter`、`GetANext`）
- 类定义（`_BuildClass`）
- 列表/元组/集合/字典构建及推导式
- 切片操作（`BuildSlice`、`BinarySubscr`）
- 模式匹配（`MatchSequence`、`MatchMapping`、`MatchKeys`、`MatchClass`）
- 导入（`ImportName`、`ImportFrom`、`_ImportAllFrom`）
- f-string 与模板字符串（`FormatSimple`、`FormatWithSpec`、`BuildString`、`BuildTemplate`、`BuildInterpolation`）
- 解包操作（`UnpackSequence`、`UnpackEx`）

### 运行时对象模型

实现了一套完整的 Python 对象模型，所有对象基于 `PyObject` 基类，通过 `PyTypeObject` 实现类型系统，支持元类自定义、C3 线性化 MRO、描述符协议等：

| 内置类型 | 说明 |
|---------|------|
| `PyObject` | 所有对象的基类 |
| `PyIntObject` | 整数（任意精度） |
| `PyFloatObject` | 浮点数 |
| `PyBoolObject` | 布尔值 |
| `PyNoneObject` | `None` |
| `PyNotImplementedObject` | `NotImplemented` |
| `PyEllipsisObject` | `Ellipsis`（`...`） |
| `PyStrObject` | 字符串（含字符串驻留池 `InternPool`） |
| `PyBytesObject` | 字节串 |
| `PyByteArrayObject` | 可变字节数组 |
| `PyListObject` | 列表 |
| `PyTupleObject` | 元组 |
| `PyDictObject` | 字典（含 `DictItems` 视图） |
| `PySetObject` / `PyFrozenSetObject` | 集合与冻结集合 |
| `PySliceObject` | 切片 |
| `PyComplexObject` | 复数 |
| `PyRangeObject` | 范围（含 `RangeIterator`） |
| `PyMemoryViewObject` | 内存视图（缓冲区协议） |
| `PyFunctionObject` | 函数 |
| `PyMethodObject` / `PyMethodWrapperObject` | 方法与包装器 |
| `PyBuiltinFunctionOrMethodObject` | 内置函数/方法 |
| `PyCodeObject` | 代码对象 |
| `PyModuleObject` | 模块 |
| `PyGeneratorObject` | 生成器 |
| `PyCoroutineObject` | 协程 |
| `PyAsyncGeneratorObject` | 异步生成器（含 `AnextAwaitable`） |
| `PyExceptionObject` | 异常（支持异常链 `__cause__`/`__context__`、异常组） |
| `PyPropertyObject` | 属性描述符 |
| `PySuperObject` | `super()` |
| `PyTypeObject` / `PyTypeObject<T>` | 类型（元类可自定义） |
| `PyCellObject` | 闭包单元 |
| `PyFileObject` | 文件对象 |
| `PyIteratorObject` / `PyStrIteratorObject` / `PyListIteratorObject` / `PyTupleIteratorObject` / `PySetIteratorObject` / `PyBytesIteratorObject` / `PyByteArrayIteratorObject` / `PyRangeIteratorObject` / `PyMemoryViewIteratorObject` | 各类迭代器 |
| `PyEnumerateObject` | 枚举对象 |
| `PyMapObject` / `PyFilterObject` / `PyReversedObject` / `PyZipObject` | 内置函数返回的迭代器 |
| `PyMemberDescriptorObject` / `PyMethodDescriptorObject` / `PyWrapperDescriptorObject` | 描述符类型 |
| `PyStaticMethodObject` / `PyClassMethodObject` | `@staticmethod` / `@classmethod` 包装器 |
| `PyTracebackObject` | 回溯对象 |

### 内置标准库模块

当前已注册的内置标准库模块（通过 `PyStandardLibrary.TryCreateModule`）：

| 模块 | 说明 |
|------|------|
| `builtins` | 内置函数（`abs`、`aiter`、`all`、`anext`、`any`、`ascii`、`bin`、`bool`、`bytearray`、`bytes`、`callable`、`chr`、`classmethod`、`compile`、`complex`、`delattr`、`dict`、`dir`、`divmod`、`enumerate`、`eval`、`exec`、`filter`、`float`、`format`、`frozenset`、`getattr`、`globals`、`hasattr`、`hash`、`hex`、`id`、`input`、`int`、`isinstance`、`issubclass`、`iter`、`len`、`list`、`locals`、`map`、`max`、`memoryview`、`min`、`next`、`object`、`oct`、`open`、`ord`、`pow`、`print`、`property`、`range`、`repr`、`reversed`、`round`、`set`、`setattr`、`slice`、`sorted`、`staticmethod`、`str`、`sum`、`super`、`tuple`、`type`、`vars`、`zip`、`__import__`）与完整的内置异常层次结构（`BaseException`、`Exception`、`TypeError`、`ValueError`、`KeyError`、`ImportError`、`OSError` 等 30+ 种） |
| `math` | 数学函数（`sqrt`、`sin`、`cos`、`tan`、`asin`、`acos`、`atan`、`atan2`、`sinh`、`cosh`、`tanh`、`asinh`、`acosh`、`atanh`、`exp`、`log`、`log2`、`log10`、`log1p`、`pow`、`ceil`、`floor`、`trunc`、`fabs`、`fmod`、`remainder`、`copysign`、`gcd`、`lcm`）与常量（`pi`、`e`、`tau`） |
| `time` | 时间函数（`time`——返回 Unix 时间戳） |
| `random` | 随机数生成（`Random` 类：`random`、`uniform`、`randrange`、`randint`） |
| `threading` | 线程支持（`Thread` 类：`start`、`join`、`is_alive`，支持 `timeout` 参数） |
| `queue` | 线程安全队列（`Queue` 类：`put`、`put_nowait`、`get`、`get_nowait`、`qsize`、`empty`、`full`、`task_done`、`join`） |
| `operator` | 运算符函数（`add`、`sub`、`mul`、`truediv`、`floordiv`、`mod`、`pow`、`lshift`、`rshift`、`and_`、`or_`、`xor`、`lt`、`le`、`eq`、`ne`、`gt`、`ge`） |
| `site` | 站点配置（自动导入，提供 `exit`、`quit` 函数） |
| `this` | Python 之禅（打印《The Zen of Python》） |

### 高级特性

- **类与继承** — 支持单继承、多继承、C3 线性化 MRO 解析
- **元类（Metaclass）** — 自定义元类、`__init_subclass__`、`__set_name__`
- **描述符协议** — `__get__`、`__set__`、`__delete__`
- **属性装饰器** — `@property`、`@staticmethod`、`@classmethod`
- **装饰器** — 函数与类装饰器
- **生成器** — `yield`、`yield from`
- **协程** — `async`/`await`、`async for`、`async with`
- **异常处理** — `try`/`except`/`else`/`finally`、异常链（`__cause__`、`__context__`、`__suppress_context__`）、异常组（`ExceptionGroup`）
- **上下文管理器** — `with` 语句
- **模式匹配** — `match`/`case` 语句（Python 3.10+）：序列模式、映射模式、类模式、通配符、字面量模式、捕获模式
- **格式化字符串** — f-string（含嵌套表达式、格式说明符）、`str.format()`、`%` 格式化
- **字符串驻留** — `PyStrObject.InternPool` 高效字符串缓存
- **导入系统** — 支持 `import`、`from ... import`、`from ... import *`、相对导入（PEP 328）、模块路径搜索（`PyModuleProvider` 可扩展）
- **C# 互操作** — 通过 `[PyType]` 等特性，支持从 Python 调用 C# 定义的类型
- **名称修饰（Name Mangling）** — 支持 `__private` 属性名称修饰
- **类型别名** — 支持 `type` 语句（Python 3.12+），通过 `PyTypeAliasTypeObject` 实现
- **虚拟文件系统** — 可替换的 `IVirtualFileSystem`，提供 `MemoryFileSystem`（内存文件系统）和 `PhysicalFileSystem`（物理文件系统）两种实现

### Roslyn 源码生成器

项目使用三个 Roslyn 源码生成器自动生成重复性代码：

**`PySharp.SourceGeneration`（公共生成器）：**

| 生成器 | 说明 |
|--------|------|
| `PyTypeGenerator` | 根据 `[PyType]` 特性自动生成 Python 类型的 C# 代码（构造、反序列化、slot 注册） |
| `PyFrozenModuleGenerator` | 将 `.py` 文件编译为冻结模块（`PyFrozenModuleObject`），嵌入程序集 |
| `PyModuleIncludeGenerator` | 根据 `[PyModuleInclude]` 属性自动生成模块加载代码 |

**`PySharp.SourceGeneration.Internal`（内部生成器）：**

| 生成器 | 说明 |
|--------|------|
| `InternalPyTypeObjectGenerator` | 为 `PyTypeObject` 生成虚拟方法分派、密封方法重写、slot 映射、特殊名称方法签名 |
| `InternalPySpecialNamesGenerator` | 自动生成 `PySpecialNames.Interned` 字符串驻留常量与枚举方法 |

### Roslyn 分析器

项目附带两组 Roslyn 分析器，帮助维护代码质量。

**`PySharp.Analyzer`（公共分析器）：**

| 规则 | 说明 |
|------|------|
| **PYSP001** | 推荐使用隐式转换代替 `FromValue()` 调用——当方法返回 `PyResult` 或 `PyResult<T>` 时，`return x;` 优于 `return FromValue(x);` |
| **PYSP002** | 当方法参数中有 `PyCallContext` 时，使用 `context.Comparer` 而非 `PyObjectComparer.Default`——后者缺少 FrameState 上下文 |

**`PySharp.Analyzer.Internal`（内部分析器）：**

| 规则 | 说明 |
|------|------|
| **PYSPI001** | 推荐使用模式匹配（`is null`、`is true`、`is 0`）代替常量相等性比较（`==`/`!=`） |
| **PYSPI002** | 控制流体（`if`/`for`/`foreach`/`while`）语句体应换行且不加花括号——禁止同行语句和不必要的块 |
| **PYSPI003** | 检测 `if`-`else if` 链中大括号风格不一致 |
| **PYSPI004** | 多行 bare statement 语句体应使用大括号 |
| **PYSPI005** | 使用 `string.Empty` 代替 `""` 字面量（模式匹配上下文中豁免） |
| **PYSPI006** | 在 `PySharp.Modules.Builtins` 命名空间中使用 `PySpecialNames.Xxx` 常量代替 `"__xxx__"` 字面量 |
| **PYSPI007** | 左花括号必须在新的一行（Allman 风格） |
| **PYSPI008** | 空类型声明应使用 C# 10+ 分号语法（`class Foo;`）代替空花括号（`class Foo { }`） |

### 虚拟文件系统

PySharp 提供可替换的虚拟文件系统抽象，用于支持文件操作和模块导入：

- **`IVirtualFileSystem`** — 文件系统抽象接口，支持目录/文件存在性检查、读写文本
- **`MemoryFileSystem`** — 内存文件系统实现，线程安全，支持多根目录，适用于测试和沙箱环境
- **`PhysicalFileSystem`** — 基于 `System.IO` 的物理文件系统实现
- **`PathHelper`** — 路径辅助类，提供 `Default`（Windows）和 `Unix` 两种路径风格

## 环境要求

- .NET SDK 10.0+
- C# 14.0+
- 支持 Trimming 和 AOT 编译

## 快速开始

```bash
# 克隆仓库
git clone https://github.com/Silencersn/PySharp.git
cd PySharp

# 构建
dotnet build

# 运行测试
dotnet test
```

### 在代码中使用

```csharp
using PySharp.Runtime;
using PySharp.Runtime.Environments;

// 方式一：静态方法快速执行代码
PyInterpreter.RunCode("print('Hello from PySharp!')");
PyInterpreter.RunCode(@"
def fib(n):
    a, b = 0, 1
    for _ in range(n):
        print(a, end=' ')
        a, b = b, a + b
fib(10)
");

// 方式二：静态方法执行 Python 文件
PyInterpreter.RunFile("script.py");

// 方式三：创建自定义环境
var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(Console.Out)
    .UseError(Console.Error)
    .Build();
using var env = PyEnvironment.CreateBuilder(host)
    .Build();
using var interpreter = PyInterpreter.Create(env);

// 执行代码
interpreter.Execute("print('Custom environment')", "<string>");
```

### 启动 REPL

```csharp
PyInterpreter.RunRepl();
```

## 与 CPython 的差异

PySharp 是一个独立的实现，并非 CPython 的完整克隆。当前已知的差异和限制：

- 部分 CPython 内部细节（如 `sys.getrefcount`、`gc` 模块）未实现
- 标准库覆盖范围有限，仅实现了常用内置模块
- 性能优化尚未完全展开
- `sys` 模块尚未完整实现

## 许可证

Copyright © 2025-2026 Silencersn. All rights reserved.
