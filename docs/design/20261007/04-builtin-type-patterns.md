# 内建类型的组织模式评审

评审基线：master 分支 88c2b740。对象：`Modules/Builtins/` 下约两百个值类型 +
元类型对的 C# 组织方式，以及这种组织对「扩展一个内建类型」的贡献者体验的
影响。槽与生成器机制分别见
[02-slots-and-dispatch.md](./02-slots-and-dispatch.md)、
[03-source-generators.md](./03-source-generators.md)。

## 类型对与 partial 拆分：主干模式

每个内建类型由「值类型 + 元类型」配对，各自再按 partial 拆分。以 `str` 为例
（`PyStrObject` 主文件 + `Intern` / `Py` / `Converter` 分部；`PyStrObjectType`
的注册代码由生成器产出），`dict` 采用「值类型主文件 + `PyDictObject.Type.cs`
专放元类型」的变体。这套约定在对象模型文档中已固化，执行也整齐——抽查各
类型，没有发现游离于约定之外的拆分。

拆分约定带来的实际收益：

- **元类型与值类型的关注点隔离彻底**。`PyDictObject.Type.cs` 里只有槽覆写与
  方法注册，值类型文件不含任何类型级样板；`dict` 的 `Or` / `IOr` 覆写
  （PR #567 新增）直接落在 `Len` 槽之后，每个覆写带 CPython 行为注释，
  review 时可以只看一个文件。
- **Python 侧方法集中在 `.Py.cs` 分部**，与协议槽（数值/容器语义）在文件
  级别分开，两类变更互不搅扰。

## 单类多型：`PyDictItemsObject` 模式

dict 的三个视图（`dict_keys` / `dict_values` / `dict_items`）与三个迭代器由
**一个 C# 类**承载，运行时按 `DefaultPyType` 区分工厂来源。这是与「类型对」
并存的第二种模式：CPython 侧三组结构体共享大量 `dictview_*` 函数，PySharp
用单类 + 三个元类型单例表达了同样的共享。

优点是消除了三份几乎相同的方法体（对比 CPython 也是宏共享）；代价是类型
安全与可读性的局部损失：类内到处出现 `self.DefaultPyType is
PyDictItemsObjectType` 的运行时判别（`ViewToTable` 的 keys/items 分支、
`ViewSymmetricDifference` 的 items 特化入口），`dict_values` 不支持的运算靠
「该元类型没接对应槽」表达。这个代价在视图家族内可控，因为三种视图本来就
共享同一份 `_dict` 与生命周期。需要防范的是模式扩散：它适合「同构家族」
（视图、迭代器），不适合语义独立只为省代码的类型。建议在贡献者文档中把
两种模式的适用边界写明（目前对象模型文档只描述了类型对模式）。

## 虚方法覆写面

`PyTypeObject<T>` 暴露 92 个 `protected virtual` 协议方法作为手写覆写面，
sealed 桥（`SubBridge` 等）在 `PyObject` 世界与强类型覆写之间做唯一一次
类型检查。这个设计的得失：

- 得：内建类型的槽实现获得 C# 级的 self 类型保证（写错立即编译错或运行时
  明确报错），且与生成器 `FillSlot(..., SubBridge)` 的接线一一对应，心智
  模型简单。
- 失：见 02 篇——强类型 self 桥与 CPython「槽函数无 self 约束」的模型差异，
  是反射合成方向反转问题的根源。此外 92 个虚方法全部挂在每个元类型实例上，
  大多数类型只覆写三五个，剩余的虚表槽位是纯开销（每个 `PyTypeObject<T>`
  实例化一次，量级无害）。

覆写面的另一个隐性收益值得记录：覆写一个槽时不需要理解槽注册机制（写
`protected override PyResult Or(...)` 即可），生成器负责其余。这是「贡献者
只面对 Python 语义、不面对管道」的设计目标，达成度 high。

## 需要收拾的几处局部

以下按影响排序，均为局部重构，不动机制：

1. **`type.__new__` 的巨型方法与内联扩展**。`PyTypeObjectType.New`
   （PyTypeObjectOfT.cs:140-362，约 220 行）承载 `type_new` 的全部阶段。
   `__slots__` 管线已经拆出（`TryResolveSlots`），但属性迁移、`__hash__`
   修正、成员安装、getset 安装、槽 fixup、`__set_name__`、`__init_subclass__`
   七个阶段仍内联在一个方法里。其中 `__class_getitem__` 的自动注入段
   （同文件 295-328 行）带有 `NOTE: AI-Generated` 来源标注与「简化范围」的
   自述：该段是 PySharp 特有扩展（CPython 无「有 `__type_params__` 就注入
   `__class_getitem__`」的对应逻辑），却插在 `type.__new__` 主流程中间。
   建议至少把该段提取为独立私有方法并补一段「为何在此注入」的语义注释，
   去掉来源标注（仓库其余代码不使用此类标注，保留会显得口径不一）；更彻底
   的是把七个阶段对齐 CPython 的 `type_new_*` 小函数族逐一提取。
2. **`DefaultGetAttribute` 与 `PyCore.GetAttrOrMethod` 双份实现**。前者源码
   注释明言「改这里要同步改那里」。两份实现的存在意味着属性查找语义的每次
   调整都有一次隐性同步义务（虚拟机的 LoadAttr 快路径一份、协议层一份）。
   中期应评估快路径能否改为「先试协议层公开的探测钩子」；短期至少把同步
   义务写进两处 `<remark>` 并考虑一个共享的语义常量表（查找顺序枚举）。
3. **`PyTypeObject` 的两个构造函数几乎全同**（PyTypeObject.cs:179-206），
   差异仅在 `Name` 的来源。可合并为一个带 `bool fromQualName` 或直接统一
   `Name = qualName.Split('.').Last()` 语义（`DefaultName` 情形下两者一致）。
4. **`FillReflectedSlots` 的 `RPow` 特判**（Init.cs:103-114）与二元族清单并列
   但形态不同（三元）。可统一为清单条目 + 三元访问器，减少一处平行代码。

## 一个正面样本：PR #567 的 dict 落地

以最近一次内建类型扩展（`dict |` / `|=` 与视图集合运算）为样本检查上述模式
的贡献者体验：`Or` / `IOr` 两个覆写共约 25 行即完成 #543 的核心（反射伴随面
`__ror__` / `__ior__` 由合成机制免费获得）；视图四运算复用 `PySetOps` 既有
批量原语，新增代码集中在语义特化（`ViewLeftDifference` 的原序差、items 的
`dictitems_xor` 转置）。扩展一个内建类型不需要触碰任何注册管道——这是
「类型对 + 虚覆写 + 生成器接线」三层架构的预期收益兑现的直接证据。

## 小结

内建类型层的组织模式整体成熟，两种类型组织模式（类型对 / 单类多型）各有
明确适用域，唯一问题是适用域未成文。局部整理（巨型 `type.__new__`、双份
属性查找、构造函数合并）都是低风险重构，可在日常迭代中顺手完成，不构成
需要专项立项的债务。
