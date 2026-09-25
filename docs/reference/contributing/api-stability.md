# 公共 API 稳定性政策

PySharp 处于 0.x 阶段，公共 API 可能调整，但库对“哪些东西是承诺、哪些是内部自由”有明确设计。本文把这条边界成文，供贡献者判断改动的影响面。

## 两类成员的设计意图

公共成员等于对使用者的承诺，改动需谨慎评估，破坏性变更要在版本提交中说明：

| 面 | 内容 |
| --- | --- |
| 执行入口 | `PyInterpreter` 全部公开成员、`Compiler.Compile` 与 `CompileMode` |
| 环境与宿主 | `PyEnvironment` / `PyEnvironmentHost` 公共成员、`IPyEnvironmentBuilder` / `IPyEnvironmentInitializationBuilder` / `IPyEnvironmentHostBuilder` |
| 文件系统 | `IVirtualFileSystem` 及 `IVirtual*Info`、`MemoryFileSystem(.Builder)`、`PhysicalFileSystem` |
| 对象与值 | `PyObject` 公共成员、各 `Py*Object` 的值构造工厂与读取访问器（速查表所列）、集合类的 C# 侧成员 |
| 结果与错误 | `PyResult` / `PyResult<T>` 及异常工厂、`PyRuntimeException` / `PyExceptionObject` 公共属性 |
| 协议操作 | `PyOperators` / `PySpecialMethods` / `Call` / `CallMethod` 扩展，可用性受上下文约束，见下 |
| 扩展模型 | `PyAttributes` 全部特性、`PyTypeObject` / `PyTypeObject<T>` 的 protected 覆写面、`PyModuleObject` / `PyFrozenModuleObject`、`PyModuleProvider.Create` |

internal 成员是实现细节，可自由重构，无兼容负担：`PyCallContext` 的构造与工厂、`PyUnwrap`、`PyEnvironment.ModuleProviders`、`Bytecode` / `Instruction` / `OpCode`、`PyInternalFrame` / `PyVariables`、`PySR`、生成的类型机器、`PyStandardLibrary`、`PyAttachedPropertiesManager`。

## 刻意设计的三个边界

改动前必读。

1. **`PyCallContext` 没有公共工厂**。协议操作 API 虽是 public 签名，但上下文只由运行时在扩展点内提供。这是“动态操作不外泄到宿主代码”的有意约束；给 `PyCallContext` 加公共构造不是小改动，而是政策变更，需要连同[差异文档](../python-compat/cpython-differences.md)与互操作指南一起评估。
2. **“实现类 internal 加公共接口”模式**。`PyEnvironmentBuilder` / `PyEnvironmentHostBuilder` 都是 internal 类配 public 接口。新增可配置对象时沿用该模式，而不是把实现类改 public。
3. **模块属性读取没有公共 API**。`PyModuleObject.PyAttributes` 为 internal，开放它同样属于政策变更，会影响[执行文档](../user-guide/executing-python.md)中“当前限制”一节的表述。

## 变更规则（0.x 阶段）

- **保持稳定优先**：README 快速开始所示路径（`RunFile` / `RunCode` / `RunRepl` / `Create` 加 `Execute`）与构建器链是最小承诺，改签名必须有充分理由；
- **优先增量**：新能力用新增成员或新重载表达，避免改既有签名；
- **可见性提升是单向门**：internal 到 public 需要按下一节的准入标准审查，并同步 [API 参考](../api/)；public 到 internal 视为破坏性变更；
- **行为变更也算变更**：公共 API 的可观察行为（协议回退顺序、错误类型、退出码）调整按破坏性变更对待，这正是回归语料存在的意义，见[测试体系](../internals/testing.md)。

## 新公共 API 的准入标准

新增 public 成员前自检：

1. **AOT 与 trimming 安全**：不引入运行时反射；需要注册机器的走“特性加源生成器”路线；
2. **错误即值**：协议层返回 `PyResult`，不在热路径抛异常，`PyRuntimeException` 保留给帧边界；
3. **惯用法合规**：通过 `PYSP*` 分析器（隐式转换、`Interned` 字段、缓存常量）；
4. **文档同步**：[API 参考](../api/)或相关 user-guide 篇目补充；
5. **命名**：符合 PYSPI009 与既有词汇，宿主侧用 `Create*` 工厂加 Builder 链，对象侧用 `From*` / `Create*` 工厂加 `Shared` 单例。

## 例外：技术上 public、政策上受限

`PyOperators` / `PySpecialMethods` / `PyObjectCallExtensions` 的成员是 public 签名，但可用性受上下文约束，拿不到 `PyCallContext` 就无法调用。它们的服务对象是扩展实现者（[扩展开发](../user-guide/extending-pysharp.md)），不构成“宿主代码可自由动态调用”的承诺，文档与代码注释都按此口径表述。
