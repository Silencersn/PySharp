# 08 · 槽位分层迁移实施记录（阶段 0-4）

本文记录 07 篇规划的阶段 0-4 的实施结果与试点经验。阶段 4（生成器
拆层与旧物删除）已完成，反射槽字段、比较六槽与槽级合成全部退役。

## 一、提交序列与验收

| 提交 | 阶段 | 内容 | 验收 |
| --- | --- | --- | --- |
| `d8210feb` | 1 | 协议骨架 `PyOperatorProtocol` + 适配视图（纯新增未接线） | 编译零警告 |
| `9c032c05` | 1 | 中间层类型 `PyOperableObjectType<T>`（C 风格虚方法族，未接线） | 编译零警告 |
| `93c3acd2` | 2 | 分发器整体切换：`PyOperators` 保留公开入口面，内部分发转发骨架；`PySpecialMethods.DivMod` 同步切换 | 全量测试零 diff |
| `b5af989a` | 2.5 | 继承链插入：`PyTypeObject<T> : PyOperableObjectType<T>` | 全量测试零 diff |
| `3994a5f8` | 3 | frozenset 试点（静态 C 风格守卫路径） | 全量测试零 diff |
| `7a1fd846` | 3 | bool 试点（继承敏感路径） | 全量测试零 diff |
| `e5fcaa34` | 3 | float 试点（桥反射回退路径） | 全量测试零 diff |
| `ddcd11d0` | 4 | S1：sealed 桥升级 SLOT1BINFULL（生成器按配对表生成反射回退形态） | 全量测试零 diff |
| `8ccc98f7` | 4 | S2：比较六槽收敛单一 RichCompare（手写槽字段 + LookupRichCompare 查名桥 + SwapComparisonOp 镜像表） | 全量测试零 diff |
| `5dbf98c5` | 4 | S3：R\* 槽字段退役——原序反射位、堆类型查名桥、FillReflectedSlots 缩减为视图合成 | 全量测试零 diff |
| `5a71010c` | 4 | S4：镜像硬编码清理核查 + 本篇阶段 4 记录 + 参考文档同步 | 全量测试零 diff |

基线与每个验收点的全量结果一致：总计 1077 / 成功 1073 / 跳过 4 /
失败 0。

## 二、落地形态

### 协议骨架与适配视图（`Runtime/PyOperatorProtocol.cs`）

骨架收拢了 `PyOperators` 原先摊在两个 18-arm switch 里的分发知识。
迁移期（阶段 1-3）前向/反射槽配对与比较镜像（Lt↔Gt、Le↔Ge）退化为
一张「op → (Forward 读取器, Reflected 读取器)」表——CPython
`_Py_SwappedOp` 的数据表形态；两个求值方向统一为：

- 左先：`Eval(left, right, F(leftType), allowReflected ? R(rightType) : null)`
- 右先：`Eval(right, left, R(rightType), F(leftType))`

（阶段 4 后该表退化为前向单列，反射位改原序调用，见
[五、阶段 4](#五阶段-4反射协议入槽与终态删除)。）

行为等价的支点：现行 `allowReflected = !eq || IsComparisonOp(op)` 已把
「比较族恒双侧」融进参数，逐 arm 数据流同构。参数位交换仍在
`EvalReflectiveOperator` 的实参序里（与旧代码同一位置）。已知语义
判型差异（P1-1）与 `DivMod` 简化协议均以 `frozen-baseline` 注释
显式标注原样保留。

### 中间层类型（`Modules/Builtins/PyOperableObjectType.cs`）

- C 风格虚方法族：`NbAdd`…`NbOr`（13 前向 + `NbDivMod`）、
  `NbInplaceAdd`…`NbInplaceOr`（13 就地）、`RichCompare(self, other, op)`
  （单一比较入口），默认实现一律 NotImplemented。
- `BridgeBinarySlot` / `BridgeTernarySlot`：SLOT1BINFULL 形态的桥工厂
  （前向守卫 → 同布局省略 → 外类型 self 的反射回退）。回退使顶层
  R\* 虚方法在 C 形态第三步可达；阶段 3 的适配视图分发期间它保持
  就绪而不被触发（反射位仍走保留的 R\* 槽），阶段 4 起生成的
  sealed 桥即此形态，工厂保留给手写接线。

### 三条试点路径

| 试点 | 迁移动作 | 验证结论 |
| --- | --- | --- |
| frozenset | Sub/And/Xor/Or → `Nb*`（双侧守卫）；Lt/Le/Gt/Ge/Eq → 单一 `RichCompare` | C 风格虚方法的双侧守卫使其参数序无关：同一 delegate 同时服务前向位、反射位（分发器已交换）与 CPython 第三步原序——06 篇「C 形态粒度更细」的实证 |
| bool | And/Xor/Or → `Nb*` | 继承敏感路径无恙：int 继承槽的合成跳过（`IsInheritedForwardSlot`）与 MRO 反射 wrapper 捡拾保持；右先分发下左操作数占 self 位由守卫正确回落（`2 & True` 走左类型前向） |
| float | 8 个前向算术槽换桥（sealed 桥 → 反射回退桥），手写 R\* 与反射槽保留 | 桥承载非平凡转发：`RAdd`/`RSub` 等 9 个手写反射经方法组接入桥回调，虚分发保留；前向位行为与 sealed 桥一致（外类型 self 从 TypeError 变为 NotImplemented+回退，该分支在现行分发下不可达） |

## 三、试点沉淀的实现约定

1. **槽 delegate 与 wrapper 同实例。** `SlotInvariantsTests` 断言前向
   槽与 `wrapper._func` 是同一 delegate 实例（`TrySetWrappedSlot` 也以
   该身份识别槽）。重接时必须单一类型化局部变量两处共用，两次方法组
   转换会产生不同实例。
2. **方法组的自然类型陷阱。** 裸方法组传入 `PyWrapperDescriptorObject`
   构造会以其自然 `Func<...>` 类型绑定 `Delegate` 参数，触发构造的
   Debug.Assert。wrapper 一律经 `PyBinaryFunction` 类型化局部变量传入。
3. **重接时机是 `PostConstruct`。** 生成的 `FillSlots`（含尾部
   `FillReflectedSlots` 合成）不可再覆写（CS0111）；`PostConstruct`
   在其后执行，合成产物先落位再被整体替换（`PyTypeObjectType` 已有
   同型先例）。
4. **合成器自动让位。** 试点类型的 R\* 槽在 `PostConstruct` 重接后
   非空，`FillReflectedSlots` 对后续构造（如 `__bases__` 变更重建）
   自动跳过——无需与合成器显式协调。
5. **虚方法转发用 this 捕获或方法组。** static lambda 无法承载实例
   虚方法分发；桥回调与槽 delegate 用捕获 this 的方法组（构造中的
   实例即 Shared 单例），子类虚覆写因此可达。

## 四、遗留与下一步

- 迁移全部完成（见五）：`PyTypeSlots` 字段与 CPython 槽结构一一对应
  （算术族无 r\* 面位、比较族单一 `RichCompare`），分发器全原序。
- 两处 P1 缺陷与已记录的语义偏差仍在冻结队列（类型相等检查走
  `__eq__` 的判型差异、`divmod` 简化协议），进入日常对齐节奏处理。
- 中间层 `Nb*` 直塞的试点残留（frozenset/bool 的四个位运算槽）保持
  双侧守卫形态不迁移——C 形态是设计内的正式面而非过渡物。

## 五、阶段 4：反射协议入槽与终态删除

阶段 4 分三个子步（S1 桥升级 → S2 比较族收敛 → S3 R\* 族收敛），
每步独立提交、独立验收零 diff。源码级的关键依据是对 CPython 3.14
`Objects/abstract.c` 的 `binary_op1` 与 `Objects/typeobject.c` 的
`SLOT1BINFULL` / `slot_nb_power` / `_PyDictView_Intersect` 的逐行核对。

### S1 · sealed 桥升级 SLOT1BINFULL（`ddcd11d0`）

`InternalPyTypeObjectGenerator` 的 sealed 桥生成分支按配对表
（Add→RAdd…Pow→RPow，14 对）输出三步形态：前向守卫（self is TObject
→ 前向虚方法）→ 同布局省略（NI 且 other is TObject → NI）→ 外类型
self 时回落 R\* 虚方法（交换回传）。此前 float 的 PostConstruct 手写
重接即此形态的试点；本步把生成器抬到同一位形，S3 删除试点重接。

### S2 · 比较族收敛 RichCompare（`8ccc98f7`）

- 六比较 dunder 离开声明清单；`PySpecialNames` 手写常量；
  `PyTypeSlots` 增加手写槽字段 `RichCompare`（`Create` 时跨基类合并）。
- 六比较虚方法保留 `[PySlot]` 作覆写检测标记（覆写触发
  `FillRichCompareSlot` 接默认桥）；bool 经 MRO 继承 int 桥——
  无条件接线会以 bool 自己的默认桥覆盖继承桥（`False < True` 回归
  即此教训），接线因此是有条件的。
- 堆类型任一比较 dunder 解析到共享查名桥 `LookupRichCompare`
  （`slot_tp_richcompare` 形态，运行时按 op 查 MRO 字典，不捕获值）。
- 分发侧 `SwapComparisonOp` 镜像表 + `EvalCompare`
  （左 (left,right,op) → 右 (right,left,swapped)）；`PyOperators.Eq/
  NotEq` 改走该入口，`Is/IsNot` 兜底保留。
- frozenset 的比较接线换到共享 delegate 的固定 op 视图
  （`FillComparisonWrapper`），并补齐 `NotEq` 的 Eq 取反路径。

### S3 · R\* 族收敛（`5dbf98c5`）

- **原序反射位**：`binary_op1` 第三步 `slotw(v, w)` 是原序调用
  （反射语义在槽内部）——分发器删除参数交换与 R\* 读取器列，
  `EvalBinarySlots`/`EvalPowSlot` 统一「两槽都按 (left, right) 原序调，
  只有槽的选取顺序不同」，并补 `slotw == slotv` 的同 delegate 跳过
  （堆-堆两类型共享查名桥时天然命中）。
- **R\* 槽字段退役**：声明清单删 14 段（槽字段、五张 switch case、
  R\* sealed 桥随之消失）；R\* 虚方法去 partial 去 `[PySlot]`，默认
  实现改为**原样转发前向虚方法**——分发回退经桥把右操作数放到
  self 位（`_PyDictView_Intersect` 的内部交换同款），覆写体即反射
  实现。
- **堆类型查名桥**：`LookupAdd`…`LookupPow`（`PyTypeObject.Default.cs`）
  复刻 `SLOT1BINFULL` 精确形态——`do_other` 要求对侧同族查名（只有
  这样的类型可能带 `__rop__`）、self 段只对同形态的 self 跑、真子类
  且反射真被覆写（`method_is_overloaded` 等价）才右先、同型 NI 不回
  退反射；pow 的 None 模数退化为两参调用（`slot_nb_power_binary`）。
  `TrySetSlot` 里十四个算术 dunder 与反射孪生合并双标签 case。
- **收敛名与槽更新机制**：`__add__`/`__radd__` 双名共享一个槽，
  `FixupAllSlots` 按名清除时需要感知孪生——`UpdateOneSlot` 清槽前经
  `GetConvergedTwinName` 询问另一拼写是否仍有提供者（首个回归：
  `A() + B()` 的 B.`__radd__` 槽被 `__add__` 名的 MRO 清空吞掉）。
- **字典视图三分工**：未覆写类型由 `FillReflectedSlots` 合成翻转视图
  （`wrap_binaryfunc_r`：`x.__rop__(y)` = `y op x`，守卫落在翻转后的
  self 位）；手写 R\* 覆写（float/complex/dict 视图/序列 RMul 等
  21 处）由 `PyTypeGenerator` 生成 `FillReflectedView` 直连覆写——
  检测经基类声明的 `[PySlot]` 符号级继承查找，覆写侧免标记（S5
  修复，见下）；继承正向槽（slot_inherited）跳过合成
  沿 MRO 捡拾基类 wrapper（bool 无自己的 `__radd__`，用 int 的）。
  默认 R\* 虚方法**不**承载属性语义（属性路径的翻转语义与分發路径
  的原序语义不同向，非交换运算下直连默认会颠倒 `x.__rsub__(y)`）——
  第二个回归即此：`(2).__rsub__(3)` 必须是 1（y-x），默认转发给的是
  x-y。
- **试点残留**：float 的 PostConstruct 重接删除（生成桥已同形态）；
  bool/frozenset 的 Nb\* 直塞保留（双侧守卫在原序第三步下左操作数
  可占 self 位，正是守卫的用武之地——`2 & True` 左类型前向兜底）。

### S3 的三个关键回归（全量 23 → 7 → 0 的修复轨迹）

1. 同型堆类 `__add__` NI 后不得回退 `__radd__`（查名桥补同型省略）。
2. 收敛双名的槽被另一名的 MRO 清空吞掉（孪生提供者检查）。
3. `x.__rsub__(y)` 的属性直调语义（视图合成与 FillReflectedView 的
   三分工）与 `(3.0).__rpow__(2)` 的三元让位（`ContainsKey` 检查
   漏在三元合成段）。

### S5 · 覆写检测回归基类标记（复核修复）

S3 删除基类 R\* 声明的 `[PySlot]`（随槽字段一起退役）后，覆写检测
的继承查找落空，当时以 21 处覆写侧手动标记补偿。复核确认这是对
`PyTypeObject<T>`「覆写即接线、零标记」承诺的回归：`PySlotAttribute`
是 `private protected`，消费程序集无法拼写它——库外类型覆写 `RAdd`
既不能自动接线（基类无标记）、也不能手动标记（不可访问），覆写
静默失效，而用户指南仍承诺 `RAdd` 可覆写。

修复回到比较族（S2）的同款形态：基类 14 个 R\* 声明恢复 `[PySlot]`
（纯检测标记，不代表槽字段存在），`PyTypeGenerator` 的
`ReflectedVirtualNames` 特判把命中的覆写分派到 `FillReflectedView`；
21 处手动标记全部删除。`ExternalTypeProtocolTests` 新增
`ProbeMirror` 探针类型——在 PySharp 之外的程序集覆写 `RAdd` 且不带
任何标记——钉住库外免标记接线（`hasattr`、属性直调、Python 子类
经 MRO 捡拾三条路径）。验收：全量 1078/1074/4/0（基线 + 该新用例），
`--no-incremental` 零警告。
