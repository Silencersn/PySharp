# 新增类型与模块：贡献者操作指南

本文面向给库本身添加能力的贡献者。在库外程序集使用同一套模型的用户视角见[扩展 PySharp](../user-guide/extending-pysharp.md)。

本文是操作清单，把散在各篇的步骤收拢为按序执行。每节末尾给出相关深入阅读。

## A. 新增内建类型（进 `builtins`）

以 `queue.Queue`（`PySharp/Modules/Queue/`）为参照模板。

1. **命名与文件**：值类型为 `Py<Name>Object`，元类型为 `Py<Name>ObjectType`，由 PYSPI009 强制；放在 `Modules/Builtins/` 或所属模块目录。Python 侧方法很多的类型用分部拆文件，惯例是 `.Py.cs` 放 `[PyMethod]` 实现，参照 `PyStrObject.Py.cs`。
2. **值类型**：字段承载数据；`public override PyTypeObject DefaultPyType => Py<Name>ObjectType.Shared;`。构造函数按需声明为 `internal`。
3. **元类型**：`[PyType("Name", Module = "builtins")]`（放其他模块时写模块名）加 `sealed partial class ... : PyTypeObject<Py<Name>Object>`。不要手写 `Shared`、私有构造、`DefaultName` / `DefaultModule` / `IsSealed`、`FillSlots()`、`RegisterMethods()` / `RegisterProperties()`，这些全部由 `PyTypeGenerator` 产出，手写会重复定义；只有 `[PyTypeConstructor(DoNotGenerateConstructor = true)]` 时才例外。
4. **方法**：`[PyMethod("名字")]` 加 `[PyFunctionParameters("a", "b=1")]`，缺后者报 PYARG010；签名为 `static PyResult M(PyCallContext, Py<Name>Object self, PyArguments)`。静态方法与类方法用 `[PyStaticMethod]` / `[PyClassMethod]`；属性用 `[PyProperty]` 三件套。
5. **构造行为**：用 `[PyExport(PySpecialNames.New, nameof(NewImpl))]` 声明 partial 属性加 `NewImpl` 实现，再覆写 `New`；支持子类化的 `obj.Value._pyType = cls` 写法用的是 internal 字段，库内可直接用。
6. **协议**：覆写 `PyTypeObject<T>` 的 `protected virtual partial` 方法（`Repr`、`Len`、`GetItem`、`Add` 等，完整清单见[自定义类型](../user-guide/custom-types.md)）；也可在构造期调用 `FillSlot`。
7. **注册进 `builtins`**：在 `Modules/Builtins/PyBuiltinsModuleObject.cs` 顶部追加一行
   `[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(Py<Name>ObjectType))]`
   该文件按“内建类型、异常层次、警告层次”分组排列，放在对应分组里。
8. **错误消息**：需要新消息时登记 `PySR`，见[错误消息规范](./error-messages.md)；不要内联字符串。PYSPI006 禁的是 `__xxx__` 字面量，消息同理集中管理。
9. **测试**：`PySharp.Tests/test_pyfiles/test_<name>.py`（断言写在 Python 内）加 `TestPyFiles.cs` 里的 `[TestMethod]`；边界与缺陷场景用 `test_<现象>_regression.py` 命名，见[测试体系](../internals/testing.md)。
10. **构建验证**：`dotnet build PySharp/PySharp.csproj` 必须通过（生成器自举，PYARG 诊断在此暴露），再跑 `dotnet test`。

> 深入阅读：[自定义类型](../user-guide/custom-types.md)、[对象模型](../internals/object-model.md)、[源生成器](../internals/source-generators.md)

## B. 新增标准库模块

以 `math`（`Modules/Mathematics/`）为参照模板。

1. **目录与文件**：`Modules/<Module>/` 下放 `Py<Module>ModuleObject.cs`；模块函数集中在 `internal static partial class Py<Module>Functions`，用 `[PyExport]` partial 属性加 `nameof` 引用实现；模块自己的类型对放同一目录，如 `Queue` 的 `PyQueueObject`。
2. **组装**：模块类上叠加 `[PyModuleInclude]`，可用 `StaticMembers`（挂全部函数）、`TypeSingleton`（挂类型）、`ExplicitMember`（挂常量，如 `math.pi`）；构造函数 `: base("<module>")`。
3. **注册**：在 `Runtime/PyStandardLibrary.cs` 的 `TryCreateModule` switch 里加一个 case，这是 `import` 能找到它的唯一入口。
4. **环境注入（可选）**：需要在 import 时从环境取状态的模块覆写 `OnImport(context, environment)`，参照 `PySysModuleObject` 注入 `argv`。
5. **纯 Python 模块的替代方案**：模块逻辑不需要 C# 时，可以做成冻结模块，即源码放 `Lib/<name>.py`（csproj 的 `AdditionalFiles` 已含 `Lib\**\*.py`），声明 `[PyFrozenModule("<name>", @"Lib\<name>.py")] public partial class Py<Name>ModuleObject : PyFrozenModuleObject;`，同样要在 `PyStandardLibrary` 注册。库内现有的冻结模块是 `this` 与 `dataclasses`。
6. **测试**：`test_<module>.py` 覆盖函数面与边界；同时更新[标准库模块覆盖](../python-compat/stdlib-modules.md)的表格。

> 深入阅读：[自定义模块](../user-guide/custom-modules.md)、[Environments 与模块解析](../internals/environments-and-modules.md)

## C. 新增异常类型

1. **声明**：在 `Modules/Builtins/PyExceptionObject.Types.cs` 按现有格式追加
   `[PyException("Name", Bases = [typeof(Py父类ObjectType)])] public sealed partial class Py<Name>ErrorObjectType : PyExceptionType;`
   文件内已有分组排列：BaseException、Exception、各家族、Warning 家族。
2. **自动获得的能力**：`InternalPyExceptionThrowGenerator` 会生成 `PyResult.Name(...)` 异常工厂与 `context.Name(...)` 抛出助手，见[源生成器](../internals/source-generators.md)；无需手写任何注册代码。
3. **builtins 注册**：在 `PyBuiltinsModuleObject` 加一行 `TypeSingleton`，放异常或警告分组。
4. **测试**：按 `test_exception*.py` 的风格补充覆盖。

## D. 新增协议槽（进阶）

协议槽是全局性改动，影响对象模型与全部类型：

1. 在 `Modules/Builtins/PyTypeObject.Declarations.cs` 按现有格式登记，形如 `[PySpecialMethod("__xxx__", typeof(Py<委托>Function))]`，需要分组时加 `SlotsMember`；生成器据此产出槽字段、非泛型与泛型虚方法以及 `PySpecialNames` 常量。
2. 在消费点接入：协议分发（`PySpecialMethods` / `PyOperators`）、VM 指令或 Emitter。
3. 语义对齐 CPython（回退规则、错误类型）并落回归测试。
4. 参考[协议分发](../internals/protocol-dispatch.md)与[对象模型](../internals/object-model.md)。

## E. 新增内建函数

在 `Modules/Builtins/PyBuiltinFunctions.cs` 加 `[PyExport("名字", nameof(Impl))]` partial 属性与实现，签名为 `static PyResult Impl(PyCallContext, PyArguments)`，参数校验经 `[PyFunctionParameters]`。函数会被 `StaticMembers` 方案自动挂进 `builtins`。同步更新[标准库模块覆盖](../python-compat/stdlib-modules.md)的函数清单。

## 收尾清单（任何一类改动通用）

- [ ] `dotnet build PySharp/PySharp.csproj` 通过，生成器自举且 PYARG / PYSP / PYSPI 诊断清零
- [ ] `dotnet test PySharp.slnx` 全量通过
- [ ] 新增 `test_pyfiles` 语料并在 `TestPyFiles.cs` 登记
- [ ] 同步受影响的文档：[内建类型速查](../api/builtin-types.md)、[标准库覆盖](../python-compat/stdlib-modules.md)、[语言特性](../python-compat/language-features.md)
- [ ] 提交信息遵循惯例，中文加 `feat:` / `fix:` / `refactor:` 前缀，见[构建与测试](./build-and-test.md)
