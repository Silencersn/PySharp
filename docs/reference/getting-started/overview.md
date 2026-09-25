# 项目概览

PySharp 是用纯 C# 从零实现的 Python 3 解释器，目标框架 .NET 10。Python 源码经完整的编译流水线
（词法分析、语法分析（AST）、语义分析、字节码编译、栈式虚拟机）处理，最终完全运行在 .NET 运行时上，
不依赖 CPython 或任何其他 Python 运行时。

核心库对裁剪（trimming）友好并兼容 AOT，可作为脚本引擎嵌入 .NET 应用程序。

## 能力范围

- 完整的编译流水线：自研词法分析器、AST 语法分析器、语义分析与字节码编译器，以及自定义的栈式字节码虚拟机。
- 内建对象模型：`int`（任意精度）、`float`、`complex`、`str`、`bytes`、`bytearray`、`list`、`tuple`、
  `dict`、`set`、`frozenset`、`memoryview`、`range`、`slice` 等，每个内建类型实现为一个
  `Py*Object` 与 `Py*ObjectType` 配对。
- 语言特性：闭包、生成器、协程与 `async`/`await`、推导式、装饰器、类与元类、描述符、`match`/`case`、
  异常组、f-string、t-string（PEP 750）、泛型与类型变量等。
- 内嵌标准库模块：`builtins`、`sys`、`site`、`operator`、`math`、`time`、`random`、`this`、
  `dataclasses`、`threading`、`queue`、`typing`、`warnings`，以及支撑 `open()` 与 `import` 的虚拟文件系统。
- 宿主 API：运行 Python 文件、代码字符串或 REPL，可控制 I/O、搜索路径与程序参数。
- 工具链：Roslyn 源生成器生成类型机制（槽位、方法描述符等），Roslyn 分析器保持代码风格一致。
- .NET 集成：目标 `net10.0`，启用 `IsTrimmable` 与 `IsAotCompatible`。

各能力面的支持现状见[语言特性支持清单](../python-compat/language-features.md)与
[标准库模块覆盖](../python-compat/stdlib-modules.md)。

## 解决方案组成

| 项目 | 角色 |
| --- | --- |
| `PySharp/` | 核心解释器库：`Compilation/`（词法、AST、字节码）、`Runtime/`（虚拟机与执行环境）、`Modules/`（内建对象与标准库） |
| `PySharp.Console/` | 命令行宿主，行为类似 `python` 命令 |
| `PySharp.Tests/` | MSTest 测试套件；`test_pyfiles/` 存放 Python 测试语料 |
| `PySharp.SourceGeneration/` | 公开的 Roslyn 源生成器（`[PyType]`、`[PyException]`、`[PyExport]` 等） |
| `PySharp.SourceGeneration.Internal/` | 库内部使用的源生成器 |
| `PySharp.Analyzer/` | 公开的 Roslyn 分析器（`PYSP*` 规则） |
| `PySharp.Analyzer.Internal/` | 库内部使用的分析器（`PYSPI*` 规则） |

## 适用场景

- 在 .NET 应用中嵌入脚本能力：把 Python 用作配置、规则、插件或胶水语言，无需分发 CPython。
- 工具与教学：借助完整的编译流水线做语法分析、字节码生成实验，或作为解释器实现的研究对象。
- 需要 AOT 或 trimming 部署，同时希望携带脚本引擎的场景。

## 当前限制

- 不追求与 CPython 100% 兼容。语言与标准库覆盖以 CPython 为参照，存在已知差异与未实现项，
  见[与 CPython 的差异](../python-compat/cpython-differences.md)。
- 标准库覆盖有限，当前以内嵌模块为主，大量 CPython 标准库模块尚不可用。
- C# 互操作为静态类型风格。库不提供对任意 C# 对象的动态绑定（类似 `dynamic` 的反射桥），
  在 C# 侧操作 Python 对象通过强类型 API 完成，见
  [在 C# 中操作 Python 对象](../user-guide/python-objects-from-csharp.md)。
- 版本为 0.x，公共 API 可能变化。

## 下一步

- [安装与构建](./install-build.md)
- [快速上手](./quickstart.md)
