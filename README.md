# PySharp

**PySharp** 是一个用 C# 从零实现的 Python 3 解释器。它包含完整的词法分析、语法分析、语义分析、字节码编译和基于栈的虚拟机执行引擎，并提供了丰富的 Python 运行时对象模型与标准库模块。

## 项目结构

```
PySharp/
├── PySharp/                              # 核心运行时库
│   ├── Compilation/                      # 编译前端
│   │   ├── Tokenization/                 # 词法分析（Lexer）
│   │   ├── AstNodes/                     # 抽象语法树（Parser）
│   │   ├── CodeAnalysis/                 # 语义分析
│   │   └── Bytecodes/                    # 字节码编译器与发射器
│   ├── Runtime/                          # 运行时
│   │   ├── Calls/                        # 调用约定与参数处理
│   │   ├── Environments/                 # 运行环境（模块、路径、导入）
│   │   ├── PyAttributes/                 # Python 类型集成特性标注
│   │   └── VirtualMachine/               # 字节码虚拟机
│   ├── Modules/                          # 标准库模块
│   │   ├── Builtins/                     # 内置类型与函数
│   │   ├── Mathematics/                  # math 模块
│   │   ├── Time/                         # time 模块
│   │   ├── Random/                       # random 模块
│   │   ├── Threading/                    # threading 模块
│   │   ├── Queue/                        # queue 模块
│   │   ├── Operator/                     # operator 模块
│   │   ├── Site/                         # site 模块
│   │   ├── This/                         # this 模块
│   │   ├── String/                       # string 模块
│   │   ├── Typing/                       # typing 模块
│   │   └── CSharp/                       # C# 互操作模块
│   ├── Utility/                          # 工具类
│   └── Resources/                        # 资源文件（字符串、语法等）
├── PySharp.SourceGeneration/             # 公共源码生成器（Roslyn）
├── PySharp.SourceGeneration.Internal/    # 内部源码生成器
├── PySharp.Analyzer/                     # 公共 Roslyn 分析器
├── PySharp.Analyzer.Internal/            # 内部 Roslyn 分析器
├── PySharp.Tests/                        # 单元测试
└── Example/                              # 示例与调试项目
```

## 功能特性

### 编译前端

- **词法分析** — 完整的 Python 3 词法扫描，支持关键字、标识符、数字字面量、字符串、缩进/取消缩进等所有标准 token 类型。
- **语法分析** — 递归下降解析器，直接生成 AST，支持 Python 3 的全部语法结构，包括模式匹配（`match`/`case`）。
- **语义分析** — 作用域解析、变量绑定、名称查找表构建。
- **字节码编译** — 将 AST 编译为自定义字节码指令序列，支持模块、表达式、交互式三种编译模式。

### 字节码虚拟机

基于栈的虚拟机，执行由编译器生成的字节码。支持丰富的指令集，包括：

- 变量加载/存储（局部、全局、闭包）
- 属性访问
- 算术与比较运算
- 控制流（条件跳转、循环）
- 函数调用与参数传递
- 异常处理（`raise`/`try`/`except`/`finally`）
- 生成器与协程
- 异步迭代器
- 类定义与元类
- 列表、元组、集合、字典构建
- 切片操作

### 运行时对象模型

实现了一套完整的 Python 对象模型，所有对象基于 `PyObject` 基类，通过 `PyTypeObject` 实现类型系统：

| 内置类型 | 说明 |
|---------|------|
| `PyObject` | 所有对象的基类 |
| `PyIntObject` | 整数 |
| `PyFloatObject` | 浮点数 |
| `PyBoolObject` | 布尔值 |
| `PyNoneObject` | `None` |
| `PyStrObject` | 字符串（含字符串驻留池） |
| `PyBytesObject` | 字节串 |
| `PyByteArrayObject` | 可变字节数组 |
| `PyListObject` | 列表 |
| `PyTupleObject` | 元组 |
| `PyDictObject` | 字典 |
| `PySetObject` / `PyFrozenSetObject` | 集合与冻结集合 |
| `PySliceObject` | 切片 |
| `PyComplexObject` | 复数 |
| `PyRangeObject` | 范围 |
| `PyMemoryViewObject` | 内存视图 |
| `PyFunctionObject` | 函数 |
| `PyMethodObject` | 方法 |
| `PyCodeObject` | 代码对象 |
| `PyModuleObject` | 模块 |
| `PyGeneratorObject` | 生成器 |
| `PyCoroutineObject` | 协程 |
| `PyAsyncGeneratorObject` | 异步生成器 |
| `PyExceptionObject` | 异常（支持完整异常链） |
| `PyPropertyObject` | 属性描述符 |
| `PySuperObject` | `super()` |
| `PyTypeObject` | 类型（元类可自定义） |
| `PyCellObject` | 闭包单元 |
| `PyFileObject` | 文件对象 |
| `PyIteratorObject` | 迭代器 |
| `PyEnumerateObject` | 枚举 |
| `PyMapObject` / `PyFilterObject` / `PyReversedObject` / `PyZipObject` | 内置函数返回的迭代器 |

### 标准库模块

当前已实现的内置标准库模块：

| 模块 | 说明 |
|------|------|
| `builtins` | 内置函数与异常（`print`, `len`, `range`, `map`, `filter`, `zip`, `enumerate`, `open`, `type`, `isinstance`, `issubclass`, `getattr`, `setattr`, `hasattr`, `delattr`, `iter`, `next`, `reversed`, `sorted`, `super`, `property`, `staticmethod`, `classmethod` 等） |
| `math` | 数学函数 |
| `time` | 时间相关函数 |
| `random` | 随机数生成 |
| `threading` | 线程支持 |
| `queue` | 队列（线程安全） |
| `operator` | 运算符函数 |
| `site` | 站点配置 |
| `this` | Python 之禅 |

### 高级特性

- **类与继承** — 支持单继承、多继承、MRO（C3 线性化）
- **元类（Metaclass）** — 自定义元类、`__init_subclass__`、`__set_name__`
- **描述符协议** — `__get__`、`__set__`、`__delete__`
- **属性装饰器** — `@property`、`@staticmethod`、`@classmethod`
- **装饰器** — 函数与类装饰器
- **生成器** — `yield`、`yield from`
- **协程** — `async`/`await`、`async for`、`async with`
- **异常处理** — `try`/`except`/`else`/`finally`、异常链（`__cause__`、`__context__`）
- **上下文管理器** — `with` 语句
- **模式匹配** — `match`/`case` 语句（Python 3.10+）
- **格式化字符串** — `f-string`、`str.format()`、`%` 格式化
- **字符串驻留** — 高效的字符串缓存
- **导入系统** — 支持 `import`、`from ... import`、模块路径搜索
- **C# 互操作** — 从 Python 调用 C# 代码

### Roslyn 源码生成器

`PySharp.SourceGeneration` 提供了源码生成器，用于自动生成 Python 类型的 C# 代码：

- `PyTypeGenerator` — 根据 `[PyType]` 特性自动生成 `PyTypeObject` 的反序列化/构造代码
- `InternalPyTypeObjectGenerator` — 内部类型对象的源码生成（虚拟方法分派、槽位映射）
- `InternalPySpecialNamesGenerator` — 自动生成 `PySpecialNames` 的枚举与字符串常量

### Roslyn 分析器

项目附带一组 Roslyn 分析器，帮助维护代码质量：

- **PYSP001** — 推荐使用隐式转换代替 `FromValue()` 调用
- **PYSP002** — 检测默认比较器（`Comparer<T>.Default` / `EqualityComparer<T>.Default`）的使用
- **PYSPI001** — 推荐使用模式匹配代替常量相等性比较
- **PYSPI002** — 检测控制流体（`if`/`for`/`while`）中缺失大括号
- **PYSPI003** — 检测不一致的 `if`-`else if` 链
- **PYSPI004** — 检测多行语句体未使用大括号

## 环境要求

- .NET SDK 10.0+
- C# 14.0+

## 快速开始

```bash
# 克隆仓库
git clone https://github.com/Silencersn/PySharp.git
cd PySharp

# 构建
dotnet build

# 运行测试
dotnet test

# 运行示例
dotnet run --project Example
```

### 在代码中使用

```csharp
using PySharp.Runtime;
using PySharp.Runtime.Environments;

// 创建运行环境
var host = new PyEnvironmentHostBuilder()
    .UseStdIO()
    .Build();
var env = new PyEnvironment(host);

// 创建解释器
var interpreter = PyInterpreter.Create(env);

// 执行 Python 代码
interpreter.RunCode("print('Hello from PySharp!')");
interpreter.RunCode(@"
def fib(n):
    a, b = 0, 1
    for _ in range(n):
        print(a, end=' ')
        a, b = b, a + b
fib(10)
");

// 执行 Python 文件
interpreter.RunFile("script.py");
```

## 与 CPython 的差异

PySharp 是一个独立的实现，并非 CPython 的完整克隆。当前已知的差异和限制：

- 部分 CPython 内部细节（如 `sys.getrefcount`、`gc` 模块）未实现
- 标准库覆盖范围有限，仅实现了常用的内置模块
- 性能优化尚未完全展开

详细的兼容性信息请参阅项目文档。

## 许可证

Copyright © 2025-2026 Silencersn. All rights reserved.
