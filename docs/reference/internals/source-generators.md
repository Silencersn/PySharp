# 源生成器与代码分析

源码：`PySharp.Roslyn.Shared/`、`PySharp.SourceGeneration/`、`PySharp.SourceGeneration.Internal/`、
`PySharp.Analyzer/`、`PySharp.Analyzer.Internal/`。

PySharp 的类型机器（slots、方法描述符、异常工厂等）全部在编译期由 Roslyn 增量源生成器产出，
运行时零反射，这是 AOT 与 trimming 兼容的前提。配套的 Roslyn 分析器在编译期强制代码风格与惯用法。

## 共享工具项目

`PySharp.Roslyn.Shared` 是与生成器并列的独立程序集，存放生成器共用的编译期工具：
`AttributeData` 读取扩展、`IndentedStringBuilder` 与 `GeneratedCode`、`ProviderExtensions`、
诊断定义与助手（`DiagnosticInfo`、`PyGeneratorDiagnostics`、`ContextExtensions`、`DebugHelper`）、
以及生成器解析类型的入口 `PySharpTypes`，命名空间相应为 `PySharp.Roslyn.Shared`、
`PySharp.Roslyn.Shared.Utility`、`PySharp.Roslyn.Shared.Diagnostics`。

它本身不是生成器也不是分析器，不单独发包，只作为裸 DLL 随主包的 `analyzers/dotnet/cs/` 一起发布。
需要它的两个生成器项目各自引用它；`PySharp.SourceGeneration.Internal` 因此不再需要引用公开生成器
程序集来借道取用工具类型——内部工具链与公开工具链之间不再有程序集级耦合。分析器项目不依赖这些
工具，保持独立。

## 生成器总览

| 生成器（全名） | 触发特性 | 产物 | 内容 |
| --- | --- | --- | --- |
| `PySharp.SourceGeneration.PyTypeGenerator` | `[PyType]`、`[PyException]` | `{类型名}.g.cs` | 元类型的 `DefaultName`、`DefaultModule`、`IsSealed`，`Shared` 单例与构造（受 `[PyTypeConstructor]` 控制），`FillSlots()`（`FillSlot` 调用），`RegisterMethods()`（`AppendMethodDescriptor`、`AppendClassMethod`、`AppendStaticMethod`，按 `[PyMethod]` 的 `Order` 排序），`RegisterProperties()`（getter、setter、deleter 合并为 `AppendMemberDescriptor`） |
| `...PyExceptionGenerator` | `[PyException]` | `{类型名}.Exception.g.cs` | 异常元类的 `Bases` 覆写 |
| `...PyExportGenerator` | `[PyExport]`（partial 属性） | `{类名}.PyExport.g.cs` | 缓存字段与 `PyBuiltinFunctionOrMethodObject.CreateFunction(...)` 的属性实现。编译期读取 `[PyFunctionParameters]`，校验实现签名必须为 `PyResult M(PyCallContext, PyArguments)` |
| `...PyFrozenModuleGenerator` | `[PyFrozenModule]` | `{类名}.PyFrozenModule.g.cs` | 构造函数与 `Code` 属性：把 `.py`（须在 csproj 的 `AdditionalFiles`，如 `Lib\**\*.py`）嵌入为 C# 原始字符串字面量，自动计算最小引号数 |
| `...PyModuleIncludeGenerator` | `[PyModuleInclude]` | `{类名}.PyModuleInclude.g.cs` | 覆写 `ApplyIncludes()`：按 `StaticMembers`、`TypeSingleton`、`ExplicitMember` 方案生成 `AppendAttribute(...)` 调用 |

生成器诊断定义在 `PyGeneratorDiagnostics.cs`，前缀为 `PYARG`，以 Error 为主：
`PYARG005` 表示冻结模块引用的 `.py` 不在 `AdditionalFiles`（Warning）；`PYARG006` 到 `PYARG009`
是 PyExport 系列错误；`PYARG010` 表示 `[PyMethod]` 缺少 `[PyFunctionParameters]`；`PYARG011`
表示 `PyTypeObject<T>` 类型参数解析失败。共同约定是显式 `null` 报诊断，而来自其他生成器产物的
不可解码常量（`TypedConstantKind.Error`）静默跳过，避免生成器间级联报错。

## 内部生成器

`PySharp.SourceGeneration.Internal` 为库自举服务，产物的元数据源是两份手写清单：

- `Modules/Builtins/PyExceptionObject.Types.cs` 有 66 个异常声明，`PyExceptionObject.Groups.cs` 分部另有
  `BaseExceptionGroup` 与 `ExceptionGroup` 两个手写声明，合计 68 个异常类型，与
  [内建类型速查表](../api/builtin-types.md)的计数口径对应。由此
  `InternalPyExceptionThrowGenerator` 生成：
  - `PyResult.ExceptionThrows.g.cs`，即 `public static PyExceptionResult TypeError(string? format,
    params ReadOnlySpan<object?> args)` 等异常工厂，也就是 [PyResult 参考](../api/PyResult.md)
    中那套 API 的来源；
  - `PyCallContext.ExceptionThrows.g.cs`，即 `internal PyRuntimeException TypeError(...)` 抛出
    助手。
- `Modules/Builtins/PyTypeObject.Declarations.cs` 是特殊方法清单
  （`[PySpecialMethod("__init__", typeof(PySelfArgsKwargsFunction))]`，可带 `SlotsMember` 分组）。
  由此 `InternalPyTypeObjectGenerator` 生成 5 个文件：
  1. `PyTypeObject.Virtual.g.cs`：`PyTypeObject` 上的虚方法占位。
  2. `PyTypeObjectOfT.Sealed.g.cs`：`PyTypeObject<T>` 上带 `self is not TObject` 检查的强类型
     密封包装。
  3. `PyTypeObject.Slots.g.cs`：`PyTypeSlots` 的委托字段（含分组）、`AllSlotNames`/`IsSlotName()`、
     `FillNullWith()`（MRO 补槽）、`TrySetSlot()`、`ClearSlot()`、`TrySetWrappedSlot()`。
     一个 dunder 名可映射多个族的槽位（slotdefs 的多对多：`__add__` → `Number.Add` +
     `Sequence.Concat`）：`TrySetSlot`/`ClearSlot` 按名合并 case，赋值填首选（Number 侧）槽并
     置空其余槽（CPython 的 sq=NULL 特判，typeobject.c:11131）；`TrySetWrappedSlot` 对多槽名以
     wrapper 委托与次槽的引用相等甄别族别，命中则保留 Sequence 侧、Number 侧保持字典驱动。
  4. `PySpecialNames.g.cs`：dunder 名常量与 `Interned` 预驻留字段。
  5. `PyTypeObjectOfT.Partial.g.cs`：`protected virtual` 协议声明，即手写覆写的目标。
- `InternalPySpecialNamesGenerator` 为手写的非生成 `PySpecialNames` 常量补 `Interned` 字段，
  与上一条的第 4 点互补。

## 分析器规则

公开包 `PySharp.Analyzer`（`PYSP*`，Warning），面向所有使用者的惯用法：

| 规则 | 要求 |
| --- | --- |
| PYSP001 | 返回 `PyResult` 时用隐式转换（`return obj;`）而非 `FromValue(x)` |
| PYSP002 | 有 `PyCallContext` 参数时用 `context.Comparer` 而非 `PyObjectComparer.Default`，后者缺帧状态会抛异常 |
| PYSP003 | 用 `PySpecialNames.Interned.X` 而非 `InternPool.FromString(PySpecialNames.X)` |
| PYSP004 | 用缓存常量（`PyIntObject.Zero`、`PyFloatObject.NaN`、`PyBoolObject.True`、`PyStrObject.Empty` 等）而非工厂调用 |
| PYSP005 | 返回类型为非泛型 `PyResult` 时直接 `return x;`，而非 `return x.ExceptionResult;`，后者经 `PyExceptionResult` 隐式转换会把成功值坍缩为 `None` |

内部包 `PySharp.Analyzer.Internal`（`PYSPI*`，Warning），约束库自身风格，在 `PySharp` 与
`PySharp.Console` 强制启用：

| 规则 | 要求 |
| --- | --- |
| PYSPI001 | 常量比较用模式匹配（`is null`、`is 0`） |
| PYSPI002、PYSPI003、PYSPI004 | 控制流语句体：单语句体无大括号且换行；if 与 else 链的大括号风格一致；跨多行的裸语句体必须加大括号 |
| PYSPI005 | 用 `string.Empty` 代替 `""` |
| PYSPI006 | `PySharp.Modules.Builtins` 内禁用 `__xxx__` 字面量，必须用 `PySpecialNames` |
| PYSPI007 | Allman 风格：`{` 独占一行，空块 `{ }` 豁免 |
| PYSPI008 | 空类型体用 `class Foo;` |
| PYSPI009 | 命名规范：`PyObject` 子类必须为 `Py<Name>Object`，`PyTypeObject` 子类必须为 `Py<Name>ObjectType`，有少量白名单豁免 |

## 接线与验证

- 接线：`PySharp.csproj` 以 `ProjectReference OutputItemType="Analyzer"
  ReferenceOutputAssembly="false"` 引用全部 4 个工具项目，并同样引用 `PySharp.Roslyn.Shared`。
  共享工具必须走 `OutputItemType="Analyzer"` 而非普通引用：编译器只在被显式传入的程序集里解析
  生成器的依赖，不会探查引用方 DLL 所在目录，普通引用会让生成器在初始化时报
  `CS8784 FileNotFoundException`。打包后 `analyzers/dotnet/cs/` 下的 DLL 由消费端 SDK 整体
  当作 analyzer 传入，与本仓库自身构建的行为一致。公开生成器、分析器与共享工具的 DLL 均以
  `Pack="true" PackagePath="analyzers/dotnet/cs"` 打进主 NuGet 包，安装包即自动获得工具链。
  `PySharp.Analyzer` 也可独立打包（`PackageId=PySharp.Analyzer`）。
- 验证方式：没有独立的生成器单元测试，依赖自举与语义回归。主库是全部生成器的最大消费者，Debug
  构建产物 `obj/.../generated/` 下有 270 余个文件，生成器回归即编译失败。语义回归见
  [测试体系](./testing.md)。生成器与分析器项目均启用 `EnforceExtendedAnalyzerRules`。
- 框架：全部生成器与分析器项目统一为 `netstandard2.0` 加 `Microsoft.CodeAnalysis.CSharp
  $(RoslynVersion)`（当前 5.0），版本收敛在 `Directory.Build.props`。生成器需要增量 API
  （Roslyn ≥ 4.0）；分析器只用经典 `DiagnosticAnalyzer`，但两者同宿共存时宿主的门槛取较高者，
  压低分析器版本并不能放宽宿主要求，因此统一到高侧，`Microsoft.CodeAnalysis.Analyzers` 同理。

## 对贡献者的影响

按任务分步的操作清单见
[新增类型与模块](../contributing/adding-types-and-modules.md)。

- 新增内建类型、异常或模块函数时的样板基本消失，写特性标注即可，模式见
  [用 C# 定义 Python 类型](../user-guide/custom-types.md)。
- 新增协议槽需要修改 `PyTypeObject.Declarations.cs` 清单，生成器据此产出一整层强类型 API。
- 新增异常类型要登记 `PyExceptionObject.Types.cs`，同时获得 `PyResult.XxxError` 工厂。
- 改动生成器本身后，先构建主库做自举验证，再跑全量测试。
