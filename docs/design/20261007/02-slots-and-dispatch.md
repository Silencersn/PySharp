# 槽系统与运算符分发评审

评审基线：master 分支 88c2b740。核心源码：`PyTypeObjectOfT.Init.cs`、
`PyTypeObject.Default.cs`、`Runtime/PyOperators.cs`；对照 CPython 3.14.6 的
`Objects/abstract.c`（`binary_op1`）、`Objects/typeobject.c`（`slotdefs`、
`SLOT1BINFULL`、`update_one_slot`）。

本篇先给出两边分发模型的精确对照，再评审两条反射建模路线的结构性差异，
最后报告两处经复现确认的正确性缺陷与一处依赖隐含约定的风险。

## 两边的分发模型

### CPython：前向槽唯一，反射在槽函数内部

CPython 3.14 的二元运算分两个层次：

1. `binary_op1`（abstract.c:929）：只认**一个前向槽**（如 `nb_add`）。三步尝试
   `w.op(v,w)[*] → v.op(v,w) → w.op(v,w)`，全部**原序**传参——右类型的槽在第三步
   被调用时，self 位收到的是**左操作数**。注释（abstract.c:919-927）明示了这一点。
   静态 C 槽（如 `dictviews_sub`）因此天然是「无 self 类型保证、双侧自检」的函数。
2. 堆类型的动态槽（`SLOT1BINFULL`，typeobject.c:9963）：`nb_add` 槽的值是
   `slot_nb_add`，它内部查 `__add__`，失败后以 `(other, self)` 查 `__radd__`
   （子类重载时先反射），即**反射逻辑封装在前向槽函数体内**。

关键事实：`slotdefs` 表中 `RBINSLOT(__radd__, nb_add, slot_nb_add, ...)`
（typeobject.c:11036）——`__radd__` 的目标槽**就是 `nb_add`**。CPython 根本没有
`nb_radd` 这样的独立反射槽字段；`__radd__` 一名只用于类型字典的 wrapper 暴露
（`wrap_binaryfunc_r` 在属性层翻转操作数）。反射槽（`nb_radd` 等字段）在
静态类型上几乎全空，`binary_op1` 也从不读它们。

### PySharp：前向槽与反射槽并列，分发层交换

PySharp 的 `PyTypeSlots.PyNumberMethods` 有独立的 `Add` / `RAdd` 字段。
分发层 `PyOperators.ReflectiveOperator` 的三档决策：

1. int × int 快速路径（`PyMath.CalculatePyIntObject`）；
2. 右类型是左类型真子类 → 先右反射槽（`RSub(right, left)`，**已交换**）；
3. 其余 → 先左前向槽、NI 后右反射槽（同样交换）；同类型时算术省略反射。

`FillReflectedSlots`（PyTypeObjectOfT.Init.cs）为静态类型合成反射槽：槽字段 =
「`self is TObject` 守卫下把 `(self, other)` 原样转发给前向槽」，属性 wrapper =
「翻转后转发」（分别镜像 `SLOT1BIN` 的反射分支与 `wrap_binaryfunc_r`）。

### 差异的根源与后果

两边对**堆类型**（Python 类）的行为收敛一致：用户定义 `__radd__` 经
`TrySetSlot` 填 `RAdd` 槽，分发层交换调用，等价于 `slot_nb_add` 内部的
`RDUNDER` 分支。

分歧出现在**静态类型作为右操作数、且前向槽依赖「self 位即左操作数」语义**的
场景。CPython 第三步 `slotw(v, w)` 把左操作数放进 self 位，`dictviews_sub` 这类
「物化 self、与 other 求差」的实现直接得到 `left - right_view` 的语义
（`{'a','c'} - d.keys()` 保留左侧内容）。PySharp 的反射槽收到的是
`(right, left)`，合成实现转发 `forward(right, left)`，算出的是反方向的差。
PR #567 为 dict 视图手写 `RSub`（`ViewLeftDifference`：把左操作数物化、减去
视图内容）正是对这一分歧的正确补偿。

这个补偿之所以必要，是因为 PySharp 的前向槽是 **sealed 桥 + 强类型 self**
（`SubBridge` 检查 `self is TObject`，不符即 TypeError），无法像 C 函数槽那样
「无类型地」接收任意左操作数。强类型 self 换来了内建槽实现的安全性，代价是
`binary_op1` 第三步的原序语义只能靠**每个非交换运算手写反射槽**来还原。

由此产生一条目前**未成文、无强制**的约定：

> 静态类型的前向二元槽若接受「任意可迭代 other」语义（对 other 无类型守卫），
> 其合成反射槽在 `x - T(y)` 场景会计算反方向的结果，必须手写反射槽。

`set` / `frozenset` 恰好不受影响，因为 `PySetObject.Sub` 等对 other 有
`is not PySetObject and not PyFrozenSetObject → NotImplemented` 的双侧守卫
（对齐 `set_sub` 的双侧 `PyAnySet_Check`），方向反转被守卫吸收为 NI，最终落
`unsupported operand type(s)` 报错，与 CPython 一致。dict 视图因 CPython 原型
（`dictviews_sub`）无 self 守卫而必须手写。也就是说，**「前向槽是否需要手写
反射」取决于它对 other 的守卫强度**，这一判定规则目前只存在于个别贡献者的
经验里。建议（按投入排序）：

1. 在贡献者文档（`adding-types-and-modules.md` 或协议分发参考）中把该约定
   写成显式规则：实现非交换二元槽时，other 无类型守卫的类型必须同时手写
   `R*` 覆写；
2. 可选的机械防护：内部分析器识别「`PyTypeObject<T>` 派生类覆写了非交换运算
   前向槽（`Sub` / `TrueDiv` / `FloorDiv` / `Mod` / `LShift` / `RShift` /
   `MatMul` / `DivMod`）且未覆写对应 `R*`」的模式并给出警告，提示贡献者
   确认 other 守卫；
3. 更彻底的路线是让非交换槽默认不合成反射视图（要求显式选择合成或手写），
   但这会改变现有 `1 - {1,2}` 等已对齐场景的行为，需要配套回归，短期不建议。

## 缺陷一：类型相等性用语义比较，元类 `__eq__` 可劫持运算分派

`PyOperators.ReflectiveOperator`（PyOperators.cs:379-390）以
`PyComparer.Eq(context, left.PyType, right.PyType)` 判定两操作数类型是否相同，
决定「子类优先」与「同类型省略反射」两条分支。CPython 的对应判定是
`Py_IS_TYPE(w, Py_TYPE(v))` 指针比较（abstract.c:945），**元类的 `__eq__`
从不参与**。语义比较带来三个后果：

1. 正确性：元类把两个不同类型判为相等时，算术运算跳过反射槽。复现
   （PySharp 报 `TypeError: unsupported operand type(s) for -: 'A' and 'B'`，
   CPython 3.14 输出 `B.rsub`）：

   ```python
   class Meta(type):
       def __eq__(cls, other):
           return True

   class A(metaclass=Meta): pass

   class B(metaclass=Meta):
       def __rsub__(self, o):
           return "B.rsub"

   A() - B()          # CPython: B.rsub
   ```

   元类 `__eq__` 抛异常时（`raise RuntimeError`），`C() + 1`（`C` 定义了
   `__add__`）在 PySharp 会在分派前就把该异常抛出，CPython 输出 `42`。
2. 性能：每个非 int 二元运算都要对类型对象做一次完整的 richcompare 协议
   调用（多数情况落到 `object.__eq__` 的引用相等，但那仍是一次槽调用加一次
   `PyResult` 包装），随后 `IsSubclassOf` 再做一次 MRO 线性扫描。CPython 是
   一次指针比较。这是所有算术/比较指令共用的热路径。
3. 概念一致性：槽系统其余部分对「类型同一」用的是引用判定
   （`IsInheritedForwardSlot` 的 `ReferenceEquals`、`PyObjectType.New` 的
   `ReferenceEquals(cls.Slots.New, Slots.New)`），唯独分派层用语义判定，
   两种「类型相等观」并存。

建议修复：`ReflectiveOperator` 中的相等判定改为 `ReferenceEquals(leftType,
rightType)`（或 `leftType == rightType`，`PyTypeObject` 未重载 `==` 时等价），
子类判定保持 `IsSubclassOf` 不变。改动局部、语义严格向 CPython 收敛，顺带
消掉热路径上的一次协议调用。同类型省略反射（`allowReflected`）随之恢复为
CPython 的「同一类型对象」语义。

## 缺陷二：`__bases__` 重赋值丢失类型自有槽

`ApplyBasesAssignment` 的提交段对子树每个类型调用 `RebuildMro`
（PyTypeObject.cs:300-305）：

```csharp
private void RebuildMro(List<PyTypeObject> baseLinearization)
{
    _mro = [this, .. baseLinearization];
    Slots = PyTypeSlots.Create(MRO.Skip(1));   // 整体替换：只重算继承合并
    ResolveTypeFlags();
}
```

`PyTypeSlots.Create` 只做基类槽的 `FillNullWith` 合并；类型**自有字典槽**
（类体中定义的 `__add__` 等经 `TrySetSlot` 写入的委托）在整体替换后丢失。
CPython 的 `type_set_bases` 提交后对整个子树跑 `fixup_slot_dispatchers`
（沿每个槽名重新从 MRO 字典解析），自有方法不受影响。复现
（PySharp 报 `TypeError: unsupported operand type(s) for +: 'B' and 'int'`，
CPython 3.14 输出 `B.add`；继承槽的重算两边一致，均为 `Q.mul`）：

```python
class Base: pass
class X(Base): pass

class B(Base):
    def __add__(self, other):
        return "B.add"

B.__bases__ = (X,)
B() + 1                       # CPython: B.add

class P:
    def __mul__(self, o): return "P.mul"
class C(P): pass
class Q:
    def __mul__(self, o): return "Q.mul"
C.__bases__ = (Q,)
C() * 2                       # 两边一致: Q.mul
```

建议修复：`RebuildMro` 之后（或 `ApplyBasesAssignment` 提交段的循环里）对子树
每个受影响类型调用 `PyTypeObject.FixupAllSlots(type)`——该函数已经存在且正是
为此语义写的（「沿 MRO 字典重新解析每个槽名」），创建路径已在用，复用即可。
注意 `FixupAllSlots` 的 `wireOwn: false` 分支会保留类型自有字典已接线的委托，
与创建路径一致，不会引入双重包装。

## 槽缓存的一致性模型

CPython 中 `tp_dict` 是单一真理源，槽只在 `update_one_slot` 被触发时惰性重解析。
PySharp 的 `Slots` 是**构造期烘焙的缓存副本**，正确性依赖三条路径协同：

1. **构造**：`PyTypeSlots.Create` 沿 MRO `FillNullWith`（按引用拷贝基类委托）；
2. **创建后修正**：`FixupAllSlots` 对每个槽名沿 MRO 字典重新解析，消除
   「烘焙的继承副本遮蔽后位基类真实方法」的错序问题；
3. **变更传播**：`UpdateSlot`（dunder 写/删于类型字典时触发）递归子类，
   自有字典遮蔽的子树被屏蔽。

这套机制的正确实现相当精巧（`UpdateOneSlot` 里 `__new__`/`__init__` 的
object 默认值特判、`__hash__ = None`、object 的 hackcheck wrapper 回接、
wrapper 描述符的引用复用，每一处都有 CPython 行号对照），但它把「缓存失效」
变成了需要人肉枚举的开放集合——缺陷二正是「`__bases__` 重赋值」这个失效点
漏掉了字典重解析。作为对照，CPython 同一场景走的是既有函数
（`fixup_slot_dispatchers`），结构上不容易漏。中期可以考虑把「任何导致 MRO
变化的提交」统一收敛到一个 `OnMroCommitted` 钩子（重建 Slots + FixupAllSlots
+ 子类传播），把三条路径的入口从三个减到一个。

另一个细节：`IsInheritedForwardSlot` 用「MRO 首个非空基类持有同一委托引用」
判定继承槽（依赖 `FillNullWith` 按引用拷贝的前提）。这是链条上的第二层隐含
约定——若未来某处改为包装式拷贝（例如给继承槽加拦截），该判定静默失真，
反射 wrapper 会错误地在子类字典重复生成。与上一节的「守卫约定」同属
「模型内不变量未成文」的范畴，建议在 `PyTypeObjectOfT.Init.cs` 的头部注释或
对象模型文档中集中陈述这些不变量。

## 其余观察

- `EvalLeftFirstReflectiveOperator` / `EvalRightFirstReflectiveOperator` /
  `InPlaceOperator` 三张 18-case 槽选择表几乎全同构，加上枚举、`OperatorToString`、
  `IsComparisonOp`，新增一个运算符要改五处。可用「`op → (前向槽访问器,
  反射槽访问器)`」的 switch 表达式收敛为一张数据表，三份分发逻辑共享。
  这是纯可维护性重构，行为不变。
- int × int 快速路径的两段前缀校验在 `ReflectiveOperator` 与
  `InPlaceOperator` 各出现一次（PyOperators.cs:313-321 与 369-377），
  可提取私有方法。
- 比较族的镜像配对（`Lt`↔`Gt`、`Le`↔`Ge`；子类时右镜像先，否则左槽先、右镜像兜底）与
  `Eq` 的双查 + 身份回退、`NotEq` 的 `Py_NE` 语义（双 NI 落身份，不查 `__eq__`）均与
  CPython `do_richcompare`（Objects/object.c）对齐。评审中发现协议分发参考文档曾把该
  顺序误述为「无条件右镜像优先、`left < right` 先试 `right.__gt__`」，已一并修正。
- `ApplySequenceFallback` 对齐 abstract 层 nb→sq 回退的四个不对称细节
  （`+` 只试左、`*` 左优先右兜底、就地版本的两级回退），实现正确。

## 小结

槽系统是 PySharp 与 CPython 差异最大、也最需要持续 vigilance 的层。当前
实现的文档化程度和注释质量在仓库内名列前茅，但强类型 self 桥与独立反射槽的
模型选择，使得「`binary_op1` 第三步原序语义」成为每个非交换二元槽都要面对的
隐形契约。两处已确认缺陷（类型相等语义化、`__bases__` 丢自有槽）都有小而
局部的修复路径。建议的优先级：先修两个缺陷（各配回归 fixture），随后把
两条隐含约定成文，槽表收敛重构放在其后。
