# 08 · 槽位分层迁移实施记录（阶段 0-3）

本文记录 07 篇规划的阶段 0-3 的实施结果与试点经验。阶段 4（生成器
拆层与旧物删除）按规划的排序决策仍排在其后，等中间层 API 经受更多
日常使用检验后启动。

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

基线与每个验收点的全量结果一致：总计 1077 / 成功 1073 / 跳过 4 /
失败 0。

## 二、落地形态

### 协议骨架与适配视图（`Runtime/PyOperatorProtocol.cs`）

骨架收拢了 `PyOperators` 原先摊在两个 18-arm switch 里的分发知识：
前向/反射槽配对与比较镜像（Lt↔Gt、Le↔Ge）退化为一张
「op → (Forward 读取器, Reflected 读取器)」表——CPython
`_Py_SwappedOp` 的数据表形态。两个求值方向统一为：

- 左先：`Eval(left, right, F(leftType), allowReflected ? R(rightType) : null)`
- 右先：`Eval(right, left, R(rightType), F(leftType))`

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
  R\* 虚方法在 C 形态第三步可达；适配视图分发期间它保持就绪而不被
  触发（反射位仍走保留的 R\* 槽）。

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

- 阶段 4 未启动（按 07 篇排序决策）：生成器拆层、R\* 槽字段与比较
  六槽删除、`FillReflectedSlots` 槽级合成退役、五张 switch 对应 case
  收敛。启动前置条件是中间层 API 冻结评审。
- 扩大迁移按试点模式逐类型进行，节奏回归日常维护带宽（原则 3/4）。
- 两处 P1 缺陷与已记录的 R\* 语义偏差仍在冻结队列，进入架构迁移后
  的日常对齐节奏处理。
