# PySharp

[English](../../README.md) | 中文

> 用 C# 从零实现的 Python 3 解释器，目标平台 .NET 10。

PySharp 是一个用纯 C# 全新编写的 Python 解释器。它将 Python 源码经过完整编译流水线 ——
**词法分析 → 语法分析（AST）→ 语义分析 → 字节码编译 → 基于栈的虚拟机** —— 并完全在 .NET
运行时上执行，不依赖 CPython 或任何 Python 运行时。核心库支持裁剪（trimming）且兼容 AOT，
可作为库嵌入到你的应用中。

---

## 功能特性

- **完整编译流水线** —— 词法分析、AST、语义分析、字节码编译，以及自研的基于栈的虚拟机。
- **丰富的内建对象模型** —— `int`（任意精度）、`float`、`complex`、`str`、`bytes`/`bytearray`、
  `list`、`tuple`、`dict`、`set`/`frozenset`、`memoryview` 等，均以 `Py*Object` / `Py*ObjectType` 实现。
- **现代语言特性** —— 闭包、生成器、协程与 `async`/`await`、推导式、装饰器、类与元类、描述符、
  `match`/`case`、异常组、f-string、泛型/类型变量等。
- **内嵌标准库模块** —— `builtins`、`math`、`operator`、`time`、`random`、`threading`、`queue`、
  `typing`、`string` 等，并提供虚拟文件系统支撑 `open()` 与模块导入。
- **可嵌入主机 API** —— 可运行 Python 文件、代码字符串或 REPL，并控制 I/O、搜索路径与参数。
- **内置工具链** —— Roslyn 分析器与源代码生成器，自动生成类型机制、保持代码风格一致。
- **现代 .NET** —— 目标 `net10.0`，`IsTrimmable` 与 `IsAotCompatible`。

---

## 快速开始

环境要求：[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。

```bash
dotnet build PySharp.slnx   # 构建
dotnet test  PySharp.slnx   # 运行测试
```

在应用中嵌入：

```csharp
using PySharp.Runtime;

PyInterpreter.RunFile("script.py");          // 运行 Python 文件
PyInterpreter.RunCode("print('hello')");     // 运行代码字符串
PyInterpreter.RunRepl();                     // 交互式 REPL
```

更多用法（自定义环境、虚拟文件系统、架构细节）可参考 `PySharp.Tests/` 测试语料与源码。

---

## 仓库结构

| 路径 | 说明 |
| --- | --- |
| `PySharp/` | 解释器核心库：`Compilation/`（词法/AST/字节码）、`Runtime/`（虚拟机与运行环境）、`Modules/`（内建对象与标准库）。 |
| `PySharp.SourceGeneration/`、`PySharp.SourceGeneration.Internal/` | Roslyn 源代码生成器（`[PyType]`、`[PyException]` 等）。 |
| `PySharp.Analyzer/`、`PySharp.Analyzer.Internal/` | Roslyn 分析器（公共 `PYSP*` 与内部 `PYSPI*` 规则）。 |
| `PySharp.Tests/` | MSTest 测试套件，`test_pyfiles/` 下为 Python 测试语料。 |
| `PySharp.slnx` | 解决方案文件。 |

---

Copyright © 2025–2026 Silencersn. 保留所有权利。

---

[English](../../README.md) | 中文
