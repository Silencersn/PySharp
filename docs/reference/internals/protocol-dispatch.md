# 协议分发

源码：`PySharp/Runtime/PySpecialMethods.cs`、`PySharp/Runtime/PyOperators.cs`、
`PySharp/Runtime/PyOperatorProtocol.cs`、`PySharp/Runtime/Comparison/`。

协议分发相当于 Python 语义的函数库：给定对象与协议名，查类型槽、调用委托、执行回退规则。
入口共享一套约定，即第一个参数为 `PyCallContext`，返回 `PyResult`，错误即值，见
[PyResult 参考](../api/PyResult.md)。

## 分发模式

以几个代表协议为例：

```text
Str/Repr:  槽(__str__) 缺失 → PyTypeObject.DefaultStr → 校验必须为 str，否则 TypeError
Bool:      NotImplemented 单例 → TypeError（真值未定义）
           其余：槽(__bool__) → 槽(__len__) > 0 → True
Iter:      槽(__iter__) → 有 __getitem__ 时包装 PyIteratorObject（序列协议），否则 TypeError
Contains:  槽(__contains__) → 迭代并逐项比较 → False
Int:       槽(__int__) → 槽(__index__) → TypeError
GetItem:   对象是类型对象时走 __class_getitem__ 或 GenericAlias，否则槽(__getitem__) 或 TypeError
```

完整回退表见 [PySpecialMethods 参考](../api/PySpecialMethods.md)。两个实现细节值得注意：

- 返回值校验集中化：`ValidateResultOf<T>` 统一检查协议返回类型（如 `__len__` 必须返回 `int`），
  报 CPython 风格的 `TypeError: ... returned non-...`。`Len` 另有额外三步检查，对齐
  `slot_sq_length`：bool 结果先归一为 `0` 或 `1`；负值报
  `ValueError: __len__() should return >= 0`；正值超出 `long` 上界报
  `OverflowError: cannot fit 'int' into an index-sized integer`。
  `range` 不参与这三步——它是 C 级的 `range_length`，长度直接经 `PyLong_AsSsize_t`，
  超出上界时报的是 `OverflowError: Python int too large to convert to C ssize_t`。
- 哈希归一化：`Hash` 把 `__hash__` 的结果经 `PyHash.HashLong` 归一化，保证实现内不变量（如相等
  对象哈希相等），无槽类型直接判不可哈希。
- 长度提示（CPython `PyObject_LengthHint` 语义，实现于 `PyUtils.LengthHint`）：`len()` 可用时
  直接采用（仅其 `TypeError` 落空），否则沿类型 MRO 查 `__length_hint__`（描述符绑定，忽略实例
  属性）调用。提示调用抛 `TypeError`、返回 `NotImplemented` 或属性不可调用时回退默认值，其余
  错误原样传播；返回非 int 报 `TypeError`，负值报 `ValueError`。消费方是 `list`（默认 8）、
  `bytes`（64）、`bytearray`（32）的构造预分配与 `operator.length_hint`。

## 运算符的反射协议

二元运算的公开入口在 `PyOperators`（`Add`/`Sub`/…，签名稳定），分发骨架收拢在
`PyOperatorProtocol`（槽位分层迁移引入，见[槽位分层](#槽位分层与中间层)与
[分层迁移实施记录](../../design/20261007/08-layering-implementation-record.md)）：
前向/反射槽配对与比较镜像退化为一张「操作符 → (前向读取器, 反射读取器)」表
（`_Py_SwappedOp` 的数据表形态），序列回退、就地族与 `divmod` 的简化协议也在此处。
骨架入口 `ReflectiveOperator` 对齐 CPython 的 `binary_op1`，按三档决策：

```text
1. int 与 int 相运算 → PyMath.CalculatePyIntObject 快速路径
   （三参 pow 的模数类型在此前置校验，NotImplemented 不外泄）
2. 右类型不等左类型且右类型是左类型的真子类
   → 先右反射槽（__radd__），返回 NotImplemented 再左槽（__add__）
3. 其余 → 先左槽，返回 NotImplemented 再右反射槽；
   但两侧类型相同时，算术运算只试前向槽、反射方法不跑（CPython binary_op1 的共享槽语义），
   比较运算保持双向
两者皆返回 NotImplemented → TypeError
```

- 合成反射槽（`PyTypeObjectOfT.Init.cs` 的 `FillReflectedSlots`，对齐 CPython
  `add_operators`）：静态类型非空的正向算术槽自动派生反射视图，但槽字段与类型字典里的
  wrapper 是**两个不同的委托**——CPython 的 `nb_add` 是原序槽（`binary_op1` 三次尝试都不
  交换操作数，实现方两侧自检），`wrap_binaryfunc_r` 只在属性层翻转；PySharp 的正向槽是
  sealed 单侧桥（self 位必须是本类型实例），无法复用「两侧自检 + 翻转」的单委托模型：
  - **槽字段**镜像 `SLOT1BIN` 的分发步——`PyOperators` 调反射槽时已交换操作数
    （`__r*(self=右操作数, other=左操作数)`），因此以 `(self, other)` 原样转发给正向槽，
    守卫 `self is TObject` 让继承槽对外来 self 退回 NotImplemented；
  - **`__r*__` wrapper** 镜像 `wrap_binaryfunc_r`——属性层 `x.__rop__(y)` 语义为
    `y op x`，翻转后转发，守卫落在翻转后的 self 位（调用方的 other）。
  手写 `R*` 覆写同时占住槽与 wrapper；继承的正向槽（`slot_inherited`）跳过合成，属性
  查找沿 MRO 落到基类的 wrapper（bool 没有自己的 `__radd__`，用 int 的）。
- 协议族回退（nb → sq）：Number 组槽都放弃后，`ApplySequenceFallback` 对齐 abstract.c 的
  abstract 层回退。`+` 只试左操作数的 `sq_concat`；`*` 先试左 `sq_repeat`，左侧没有才试右侧
  （`2 * [1]` 即走右侧，以 `(right, left)` 调用）；就地版本依次
  `sq_inplace_concat → sq_concat`、`sq_inplace_repeat → sq_repeat`。
- 槽位协议族：`PyTypeSlots` 按族分组（`Number`、`Sequence`），`__add__`/`__mul__`/`__iadd__`/
  `__imul__` 一名双槽（CPython slotdefs 的多对多映射）。堆类型定义这些 dunder 时填 Number 侧并
  置空 Sequence 侧（typeobject.c:11131 的 sq=NULL 特判，副作用由上面的 nb→sq 回退吸收）；原生
  序列类型（str/bytes/bytearray/list/tuple）只填 Sequence 侧；删除覆盖后按 wrapper 委托与
  Sequence 槽的引用相等甄别族别并恢复（`PyWrapperDescrObject.d_base->wrapper` 签名匹配的等价物）。
  原生序列类型因此没有 `__radd__`（sq_concat 无反射变体），`__rmul__` 由手写 RMul 覆写
  （非换序的 wrap_indexargfunc 语义）提供。
- match 语句的 sequence/mapping 判定与槽位无关：纯类型 flag（`PyTypeFlags.Sequence/Mapping`，
  对齐 `Py_TPFLAGS_*` 的位值），构造期查静态表并沿 MRO 继承；str/bytes/bytearray 与自带
  `__getitem__` 的用户类因无 flag 而不匹配序列模式。
- 比较族（`Lt` 与 `Gt` 族）：镜像配对（`Lt`↔`Gt`、`Le`↔`Ge`），对齐 CPython
  `do_richcompare` 的「交换参数加交换操作码」：右类型是左类型的真子类时先
  `right.__gt__(right, left)`，否则先 `left.__lt__(left, right)`，返回
  `NotImplemented` 再试 `right.__gt__(right, left)`；与算术不同，不受同类型省略的影响。
  `Eq` 是特例，反射查询两侧都查 `__eq__`，均返回 `NotImplemented` 时退化为引用相等。
- 就地运算（`InPlaceOperator`）：先试 `__iadd__` 家族，返回 `NotImplemented` 再回退普通二元运算，
  回退时错误消息用增强形式拼写。
- 一元运算（`UAdd`、`USub`、`Invert` 对应 `__pos__`、`__neg__`、`__invert__`）：槽存在时结果原样
  透传，槽返回 `NotImplemented` 单例本身也照传。CPython 的一元槽包装不检查返回值，这与二元槽
  「返回 `NotImplemented` 表示放弃」的约定不同。槽缺失才报 `TypeError`。
- 错误消息按运算符族分模板，对齐 CPython 的 `binop_type_error`：

  | 族 | 模板 |
  | --- | --- |
  | 算术 | `unsupported operand type(s) for {op}: '{t1}' and '{t2}'` |
  | 比较 | `'{op}' not supported between instances of '{t1}' and '{t2}'` |
  | 一元 | `bad operand type for unary {op}: '{t}'` |

  其中 `**` 显示为 `** or pow()`，就地运算显示 `+=` 或 `**=` 等增强形式。
- 属性访问：`GetAttr` 两段式，先 `__getattribute__`，结果为 `AttributeError` 且定义了 `__getattr__`
  时才调用后者。`string` 名字重载先经环境 `InternPool` 驻留再进入查找。

## 槽位分层与中间层

槽位分层重构（[提案评估](../../design/20261007/06-slot-protocol-layering-proposal.md)、
[实施记录](../../design/20261007/08-layering-implementation-record.md)）在
`PyTypeObject` 与 `PyTypeObject<T>` 之间插入了中间层
`PyOperableObjectType<T>`（`Modules/Builtins/PyOperableObjectType.cs`）：

- **C 风格虚方法族**：`NbAdd`…`NbOr`（前向二元）、`NbInplaceAdd`…（就地族）与单一的
  `RichCompare(self, other, op)`。接收者 `self` 为 `PyObject`，无类型承诺——双侧守卫与
  NotImplemented 回退是实现者的显式责任（CPython 静态槽如 `set_sub` 的双侧
  `PyAnySet_Check` 正是此形态）。双侧守卫使入口参数序无关，同一 delegate 可同时服务
  前向位、反射位（分发器已交换）与 CPython `binary_op1` 第三步的原序调用。
- **反射回退桥工厂**：`BridgeBinarySlot` / `BridgeTernarySlot` 是 `SLOT1BINFULL`
  （`slot_nb_add`）的面向对象等价物：前向守卫转发，NotImplemented 后按同布局省略，
  外类型 `self`（原序第三步的形态）时回落到反射虚方法。回退使顶层 R\* 覆写在 C 形态
  下可达；适配视图分发期间它保持就绪而不被触发（反射位仍走 R\* 槽）。

迁移按行为冻结原则分阶段进行：分发骨架经**适配视图**读取现有槽字段（行为与迁移前
零差异，已知语义判型差异与 `divmod` 简化协议原样冻结）；已迁移的试点类型
（frozenset、bool、float）在 `PostConstruct` 中把槽重接到 C 形态入口，合成器
`FillReflectedSlots` 见 R\* 槽非空自动让位，其余类型仍走生成器接线与合成。
R\* 槽字段、比较六槽与槽级合成的退役排在生成器拆层之后（迁移阶段 4）。

## 比较器

`Runtime/Comparison/` 下的类型：

| 类 | 职责 |
| --- | --- |
| `PyComparer` | `Eq`、`NotEq`、`Lt`、`LtE`、`Gt`、`GtE` 的统一入口，把委托结果转 `PyBoolObject`；容器排序（`sorted`、`list.sort`）复用 |
| `PyCollectionComparer` | 集合与序列的逐元素比较，即字典序语义 |
| `PyObjectComparer` | 与 `PyCallContext` 绑定的比较器对象（`context.Comparer`） |
| `PyObjectConstEqualityComparer` | 常量池去重用的 `IEqualityComparer<PyObject>`，同一性对齐 CPython `_PyCode_ConstantKey`（类型参与，float/complex 按位模式，元组按元素递归，其余按 `==`），服务 `BytecodeBuilder` 的常量池 |

## 修改协议行为的位置

- 给内建类型加协议：在元类型上覆写 `protected virtual` 方法，或 `FillSlot`，见
  [对象模型](./object-model.md)；运算协议可改覆写中间层
  `PyOperableObjectType<T>` 的 C 风格入口（`Nb*`/`RichCompare`，试点形态，
  见[槽位分层](#槽位分层与中间层)）。
- 用户定义类的 `__repr__` 等在类体中定义，由类型创建路径把它们登记进类型属性与槽。
- 新增全局协议入口：在 `PySpecialMethods` 加分发方法并加对应槽字段，注意与 CPython 的回退行为
  对齐，并补 `test_pyfiles` 回归。
