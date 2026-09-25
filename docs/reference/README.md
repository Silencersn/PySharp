# PySharp 文档

本文档对应 PySharp 0.50，描述当前已提交的稳定行为。行为随版本演进，与文档不符时以实现为准。

PySharp 是用纯 C# 从零实现的 Python 3 解释器，目标框架 .NET 10。Python 源码经完整的编译流水线
（词法分析、语法分析、语义分析、字节码编译、栈式虚拟机）执行，全程运行在 .NET 运行时之上，
不依赖 CPython 或任何其他 Python 运行时。核心库对裁剪（trimming）友好并兼容 AOT，可嵌入 .NET 应用。

文档面向两类读者：

- 库使用者：在 .NET 应用中嵌入 PySharp、执行 Python 代码、与 Python 对象互操作的开发者。
- 贡献者：理解解释器内部实现并参与开发的开发者。

## 阅读路线

### 嵌入者

按顺序阅读入门三篇，再按需查阅使用指南：

1. [项目概览](./getting-started/overview.md)：PySharp 的能力范围与当前限制。
2. [安装与构建](./getting-started/install-build.md)：环境要求、构建命令、CLI 用法。
3. [快速上手](./getting-started/quickstart.md)：执行第一段 Python 代码。
4. [执行 Python 代码](./user-guide/executing-python.md)：便捷 API 与受控环境两层模型。
5. [配置执行环境](./user-guide/environment.md)：搜索路径、程序参数与初始化选项。
6. [宿主与 I/O 重定向](./user-guide/hosting.md)：接管 stdin、stdout、stderr。
7. [虚拟文件系统](./user-guide/virtual-file-system.md)：内存 FS、物理 FS 与 `open()`、`import`。
   文件对象的方法面见[文件对象](./user-guide/file-objects.md)。
8. [命令行与交互模式](./user-guide/cli-and-repl.md)：`pysharp` 选项、退出码与 REPL 行为。
9. [错误处理](./user-guide/error-handling.md)：Python 异常在 C# 侧的呈现方式。
10. [在 C# 中操作 Python 对象](./user-guide/python-objects-from-csharp.md)。
11. [运算符与协议分发](./user-guide/operators-and-protocols.md)。
12. [扩展 PySharp](./user-guide/extending-pysharp.md)：以 attribute 声明式暴露 C# 能力的总览。
13. [用 C# 定义 Python 类型](./user-guide/custom-types.md)。
14. [用 C# 编写 Python 模块](./user-guide/custom-modules.md)。
15. [警告与数据类](./user-guide/warnings-and-dataclasses.md)：`warnings` 过滤与 `@dataclass`。

各类型的逐成员说明见 [API 参考](./api/)。具体问题可先查[常见问题](./user-guide/faq.md)、
[场景手册](./user-guide/howtos.md)与[术语表](./glossary.md)。

### 兼容性速查

评估一段脚本能否在 PySharp 上运行时，查阅以下四篇：

- [语言特性支持清单](./python-compat/language-features.md)
- [内建类型方法面覆盖](./python-compat/builtin-type-methods.md)
- [标准库模块覆盖](./python-compat/stdlib-modules.md)
- [与 CPython 的差异](./python-compat/cpython-differences.md)

### 贡献者

先读[总体架构](./internals/architecture.md)，再按子系统深入 [internals](./internals/)。
动手前应过一遍流程类内容：

- [新增类型与模块](./contributing/adding-types-and-modules.md)：添加内建类型、标准库模块、异常与协议槽的分步清单。
- [调试指南](./contributing/debugging.md)：分层定位、断点建议与生成代码的阅读方式。
- [错误消息规范](./contributing/error-messages.md)：`PySR` 常量的组织与新增流程。
- [公共 API 稳定性政策](./contributing/api-stability.md)：public 与 internal 的边界及变更规则。
- [构建与测试](./contributing/build-and-test.md)、[编码规范](./contributing/coding-standards.md)、
  [发布流程](./contributing/release-process.md)、[路线图](./contributing/roadmap.md)。

## 分区总览

每个分区都有一篇 `README.md`，说明分区定位、阅读路径与逐篇索引。

| 分区 | 内容 |
| --- | --- |
| [getting-started](./getting-started/README.md) | 入门：项目概览、安装构建、快速上手 |
| [user-guide](./user-guide/README.md) | 使用指南：执行、环境、I/O、文件系统、错误处理、对象互操作、声明式扩展、FAQ |
| [api](./api/README.md) | 核心公共 API 的逐成员参考，附内建类型速查表 |
| [python-compat](./python-compat/README.md) | Python 语言与标准库的支持现状，与 CPython 的差异 |
| [internals](./internals/README.md) | 实现内幕：编译流水线、虚拟机、对象模型、各子系统 |
| [contributing](./contributing/README.md) | 贡献流程：构建测试、编码规范、调试、发布、路线图 |

[术语表](./glossary.md)汇总跨受众的中英术语。性能特征见
[internals/performance.md](./internals/performance.md)。

## 约定

- 示例中的 C# 代码依据库的公共 API 源码撰写，命名空间根为 `PySharp`。
- Python 代码示例以 PySharp 的实际行为为准，可能与 CPython 存在差异，
  见[与 CPython 的差异](./python-compat/cpython-differences.md)。
- 引用源码位置时使用仓库相对路径，例如 `PySharp/Runtime/PyInterpreter.cs`。
