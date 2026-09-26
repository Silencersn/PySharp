# 使用指南

本分区面向库使用者，即在 .NET 应用中嵌入 PySharp 的开发者。内容按主题分五组，逐成员的参考请配合
[api](../api/README.md) 使用。

## 阅读路径

- 嵌入主线，按 1 到 6 的顺序阅读：执行模型、环境配置、I/O 接管、文件系统、错误处理、对象互操作。
- 扩展主线，按需阅读：先读[扩展 PySharp](./extending-pysharp.md)，再进自定义类型与自定义模块两篇。
- 任务导向：遇到具体问题直接查[场景手册](./howtos.md)或[常见问题](./faq.md)。

## 篇目索引

执行与环境：

| 篇目 | 内容 |
| --- | --- |
| [执行 Python 代码](./executing-python.md) | 两层执行 API（静态便捷方法与 `Create` 加 `Execute`）、`MakeModule`、会话状态共享、当前限制 |
| [配置执行环境](./environment.md) | `IPyEnvironmentBuilder` 全选项（编码、优化级、颜色、`NotImplyImportSite`）、释放与退出流程、`sys.path` 与 import 解析顺序 |
| [宿主与 I/O 重定向](./hosting.md) | `PyEnvironmentHost` 的宿主矩阵、构建器方法、捕获输出、喂入输入、沉默模式 |
| [虚拟文件系统](./virtual-file-system.md) | `IVirtualFileSystem` 抽象、`MemoryFileSystem` 构建器、在内存中提供可 import 模块 |
| [文件对象](./file-objects.md) | `open()` 的模式全集与错误映射、方法与属性面、文本与二进制模式、迭代与 `with` |
| [命令行与交互模式](./cli-and-repl.md) | `pysharp` 选项逐项参考、退出码约定、REPL 多行判定与表达式回显、嵌入侧 `RunRepl` |

错误与互操作：

| 篇目 | 内容 |
| --- | --- |
| [错误处理](./error-handling.md) | `PyRuntimeException` 与 `PyExceptionObject` 公共面、按类型检查、读取 `Args`、`SystemExit` 退出码转换 |
| [在 C# 中操作 Python 对象](./python-objects-from-csharp.md) | 值构造与读取总表、集合的 C# 侧成员、协议操作的上下文边界 |
| [类型映射与转换](./type-mapping.md) | Python 与 C# 的完整映射表、无隐式转换的边界、常见互操作陷阱 |
| [运算符与协议分发](./operators-and-protocols.md) | `PyResult` 错误即值模式、`PyOperators` 反射协议与子类优先、`PySpecialMethods` 回退表、调用扩展 |

声明式扩展：

| 篇目 | 内容 |
| --- | --- |
| [扩展 PySharp](./extending-pysharp.md) | 总览入口：attribute 到源生成器到类型机器的工作模型、特性速查、签名约定、`geom` 端到端示例、注册与可见性 |
| [用 C# 定义 Python 类型](./custom-types.md) | 类型对全集、方法与属性、构造、协议覆写、`[PyException]` |
| [用 C# 编写 Python 模块](./custom-modules.md) | 纯 Python 模块与 C# 模块对象两条路、`[PyModuleInclude]` 三种方案、冻结模块、`PyModuleProvider` 与提供器链定制 |
| [警告与数据类](./warnings-and-dataclasses.md) | `warnings` 的过滤动作与 `catch_warnings`、`@deprecated`、`@dataclass` 的字段控制与未覆盖面 |

兼容性相关的用户视角结论（支持哪些语法与模块、与 CPython 的差异）在
[python-compat](../python-compat/README.md)。

横向参考：

| 篇目 | 内容 |
| --- | --- |
| [常见问题与故障排查](./faq.md) | 按问答组织的嵌入、互操作、扩展与兼容性高频问题，附按现象分层的排查流程 |
| [嵌入场景手册](./howtos.md) | 七个任务式场景：取结果、异常传数据、沙箱、插件式模块、多会话、声明式暴露、REPL |
