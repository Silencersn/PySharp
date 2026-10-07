# 源生成器架构评审

评审基线：master 分支 88c2b740。源码：`PySharp.SourceGeneration/`（公开生成器）、
`PySharp.SourceGeneration.Internal/`（自举生成器）、`PySharp.Roslyn.Shared/`
（共享工具）。配套文档：[源生成器与代码分析]
(../../reference/internals/source-generators.md)。

## 总体形态：清单驱动的三层工厂

类型机器的生成链是本仓库最有辨识度的架构决策，分层如下：

```text
PyTypeObject.Declarations.cs          特殊方法清单（92 个 dunder，手写、单一真理源）
        │  InternalPyTypeObjectGenerator
        ▼
PyTypeObject.Virtual.g.cs             基类 *Bridge 虚方法占位（PyObject 签名）
PyTypeObjectOfT.Sealed.g.cs           泛型侧密封桥（self is TObject 检查后转发）
PyTypeObjectOfT.Partial.g.cs          92 个 protected virtual 强类型协议声明
PyTypeObject.Slots.g.cs               槽委托字段 + FillNullWith/TrySetSlot/ClearSlot/…
PySpecialNames.g.cs                   dunder 名常量与预驻留
        │  PyTypeGenerator（[PyType]/[PySlot]/[PyMethod]/[PyProperty]）
        ▼
{每个内建元类型}.g.cs                 FillSlots / RegisterMethods / RegisterProperties
```

对这个形态的评价：

- **单一真理源执行得彻底**。新增一个协议槽只改 `Declarations.cs` 一处，
  生成器据此产出从声明、桥接、槽字段到按名 set/clear 的整层 API；新增内建类型
  只写特性标注。两个方向都没有第二处需要同步的手写样板，这直接回答了
  「92 个槽 × 200 余个类型」为何还能维持一致性。
- **清单（手写 partial 声明）而不是反射/配置文件**作为元数据载体，让生成器
  的输入与被生成的 API 在同一个文件里相邻，可读性和review半径都好。
- **增量管线正确**。`ForAttributeWithMetadataName` + `Collect` + `Combine`，
  transform 内无捕获、诊断以值（`DiagnosticOr<T>`）穿透，符合增量生成器的
  无状态约束。诊断前缀 `PYARG` 分级明确，「显式 null 报诊断、不可解码常量
  静默跳过」的约定避免了生成器间级联报错。
- **自举验证策略务实但有缺口**：没有生成器单元测试，回归靠主库编译 +
  270 余个生成文件 + 语义测试兜底（文档如实声明）。这个选择在当前规模下
  成立——生成器 bug 几乎必然表现为编译失败——但「生成结果在语义上错了而
  仍能编译」的情况（例如漏生成一个 `FillReflectedSlots` 调用）只能靠 Python
  级测试撞见。如果生成器数量或复杂度继续增长，值得为 `PyTypeGenerator` 的
  `GenerateSource` 补最小快照测试（输入一个小型特性化类型，断言产出文本）。

## 运行时接线面

生成代码与手写运行时的接缝设计良好：

- 生成器只调用 `PyTypeObject<T>` 暴露的 `protected` 注册 API
  （`FillSlot` / `FillNewSlot` / `AppendMethodDescriptor` / `AppendClassMethod` /
  `AppendStaticMethod` / `AppendMemberDescriptor`），全部标注
  `EditorBrowsable(Never)`。运行时若改注册语义，生成面自动跟随。
- `FillSlots()` 末尾固定调用 `FillReflectedSlots()`——反射槽合成是运行时逻辑
  （需要 MRO 与 `IsRuntimeCreated` 判定），放在运行时而非生成期是正确的
  分界；生成器对它只知道「要在最后调一次」。
- `FillNewSlot()`、`__class_getitem__` 的 classmethod 注册等少量场景在生成器
  里有专门分支（`slot.Name is "New"`），属于生成器里仅有的两个「知道具体槽
  名」的地方，收敛得好。

## 模板层的已知瑕疵

以下均为小项，不影响正确性，列出以便顺手清理：

1. **族的重复播种**。每个带 `SlotsMember` 的槽前都生成一行
   `Slots.Number ??= new();`（`PyDictObjectType.g.cs` 中 Or 与 IOr 各带一行）。
   幂等所以正确，但组内去重只需生成器侧一次 `DistinctBy(slot.SlotsMember)`。
2. **同名 hint 的二次 `AddSource` 风险**。`PyTypeGenerator.Initialize` 分别收集
   `[PyType]` 与 `[PyException]` 两个 provider 后合并；一个同时标注两个特性的
   类型会被收集两次，产出同名 `{类型名}.g.cs` 两次，`AddSource` 抛异常。当前
   代码库没有这种类型，属于潜在坑而非现实 bug，加一个按符号去重即可永久关闭。
3. **属性访问器的静默覆盖**。`[PyProperty]` 按 Python 名分组 getter/setter/deleter
   时直接赋值，同名同角色的第二个声明静默取代第一个，无诊断。低概率、低危害，
   可与 2 一并处理。
4. **`PyTypeInfo.TypeArgName` 解析失败时的半生成**。类型参数解析失败报
   `PYARG011` 后仍继续生成（方法注册循环里判空跳过），产出残缺文件。与
   「显式错误早停」的主约定略有不齐，但这是 Warning 级且主库自举覆盖，
   优先级最低。

## 反射槽合成：生成期与运行时的边界

`FillReflectedSlots` 位于 `PyTypeObjectOfT.Init.cs`（运行时），由生成代码在
`FillSlots` 尾部调用。它的 13 行合成清单（`RAdd` 到 `ROr`）同时包含交换律
运算与非交换运算（`RSub`、`RDivMod` 等）。如
[02-slots-and-dispatch.md](./02-slots-and-dispatch.md) 所析，非交换槽的合成
只在「前向槽对 other 有类型守卫」时安全。这份清单本身是「哪些槽默认获得
合成反射」的唯一开关，但它埋在运行时实现里，与 `Declarations.cs` 的清单
视角割裂——读清单的人不知道 `__rsub__` 有合成行为，读 `Init.cs` 的人看不到
全貌。若采纳 02 篇的「非交换槽默认不合成」路线，这个开关应显式化（例如
`Declarations.cs` 的 `PySpecialMethodAttribute` 增加 `Reflected` 命名参数，
由生成器决定是否请求合成）；即使维持现状，也建议在 `Declarations.cs` 头部
注释中说明反射合成的存在与边界。

## 分析器与风格约束

配套的三层分析器（`PYSP*` 公开惯用法 / `PYSPI*` 内部领域约定 / `PYSPS*`
通用风格）与本篇主题相关的一点：**它们守护的正是前两篇指出的「隐含约定」
的近邻**。`PYSP002`（必须用 `context.Comparer`）与 `PYSPI001`（禁 `__xxx__`
字面量）都在把运行时不变量变成编译期约束。沿着同一思路，02 篇建议的
「非交换槽缺 `R*` 覆写」警告有现成的落点（`PYSPI` 家族），实现成本低于
文档约定且不可绕过——这是把「模型内不变量」成文化的最佳通道。

`PySharp.Analyzer.Style` 的自举接线（预构建 dll + `_PyspsSelfCheck` 守卫的
子构建 + 独立 `IntermediateOutputPath`）解决了循环依赖与增量构建两个深坑，
文档对坑的成因记录完整，属于工具链里经得起复盘的设计。

## 小结

生成器层是仓库中「用编译期工作换运行时一致性」执行得最完整的部分，三层
工厂的职责切分与单一真理源值得作为后续扩展的范式。待办按优先级：
（1）`AddSource` 去重与属性访问器覆盖诊断（半小时级）；（2）反射合成开关的
显式化（与 02 篇联动）；（3）族播种去重（顺手）；（4）`PyTypeGenerator`
最小快照测试（规模增长后再议）。
