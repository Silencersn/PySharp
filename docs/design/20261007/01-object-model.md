# 对象与类型模型架构评审

评审基线：master 分支 88c2b740（含 PR #566、#567）。对照实现为 CPython 3.14.6
源码（`Objects/typeobject.c`、`Objects/object.c`）。本篇覆盖 `PyObject` / `PyTypeObject`
的 C# 建模、实例布局代数与属性存储；槽系统与运算符分发见
[02-slots-and-dispatch.md](./02-slots-and-dispatch.md)，生成器见
[03-source-generators.md](./03-source-generators.md)。

## 概念映射总表

PySharp 没有照搬 CPython 的 C 结构体布局，而是把每个 C 层概念翻译为一个 C# 机制。
整体映射关系清晰，绝大多数语义点可以在两边互查：

| CPython | PySharp | 评注 |
| --- | --- | --- |
| `PyObject` 头（refcnt + `ob_type`） | `PyObject` 类 + `_pyType` 字段 | 引用计数交给 CLR GC，`PyId` 用弱表分配 |
| `PyTypeObject` C 结构体 | `PyTypeObject`（身份/MRO）+ `PyTypeSlots`（协议委托）+ `PyAttributes`（`tp_dict`） | 一分为三，职责边界干净 |
| `tp_as_number` / `tp_as_sequence` 嵌套结构体 | `PyTypeSlots.PyNumberMethods` / `PySequenceMethods` 嵌套类 | 族的分组方式一致；族外槽保持直字段 |
| 静态类型 vs 堆类型（`Py_TPFLAGS_HEAPTYPE`） | `Shared` 单例 + `IsRuntimeCreated=false` vs `UserDefinedType<T>` | 静态类型在 CLR 侧同样静态（类型初始化器构造） |
| `tp_mro` / `tp_base` / `tp_subclasses` | `InternalMRO`（span 视图）/ `Bases` / `List<WeakReference<PyTypeObject>>` | 子类弱注册带惰性修剪，静态类型不注册（理由见源码注释，成立） |
| `tp_flags` 位标志 | `PyTypeFlags`（仅模式匹配位）+ 一组布尔虚属性 | **一拆为二**，见下文「标志的 OOP 化」 |
| `tp_basicsize` / `tp_itemsize` / `solid_base` / `best_base` | `LayoutType`（CLR `Type`）+ `IsSubclassOf` / `IsAssignableFrom` 判定 | **布局代数借道 CLR 类型系统**，见下文 |
| `tp_dictoffset` / managed dict | `PyObjectManagedDict._pyAttributes` 字段 vs `ConditionalWeakTable` | 双轨存储，按类型选择 |
| `tp_dealloc` / `tp_free` 的分配域差异 | `ReleasesWithFreeList` 布尔 | 只为 `__bases__` 赋值的兼容性检查建模，够用 |
| `tp_name` vs `ht_qualname` vs `__module__` | `TpName` / `QualName` / `Name` / `ReprName` / `FullyQualifiedName` | 五个命名面与 CPython 的 `%T`、`%R` 消费点逐一对应，注释带行号 |

命名面（`TpName`、`FullyQualifiedName` 等）是这类移植里最容易失真的部分，当前
实现把每个名字的消费场景（错误消息、repr、哈希失败措辞）都溯源到了 CPython
行号，质量高。

## 布局代数：借道 CLR 类型系统

CPython 用 `tp_basicsize`、`tp_itemsize` 与 `solid_base` 的偏序决定多继承的布局
兼容性（`best_base`、`compatible_for_assignment`）。PySharp 的做法是把「实例的
物理形状」编码为 CLR 继承链：`LayoutType => typeof(TObject)`，布局兼容性判定
直接复用 `Type.IsSubclassOf` / `IsAssignableFrom`
（`PyTypeObject.ValidateBasesAndResolveLayoutTypeOwner`，背景见
[对象模型](../../reference/internals/object-model.md)）：

```text
PyObject                    —— 无实例字典（int/str/tuple 等不可变值类型挂在这层）
└── PySlotsObject           —— __slots__ 排除实例字典的哨兵布局
    └── PyObjectManagedDict —— 带惰性实例字典（模块、异常、用户类实例）
```

这个决策值得肯定的一面：布局判定从一个手写偏序算法变成 CLR 编译器已经保证
的性质，`SolidBaseOf` / `StrongestLayoutOf` / `AnchorsLayout` 三个小函数就完成了
CPython 数百行布局协商的核心，且静态正确。`__bases__` 重赋值的完整协商
（`ApplyBasesAssignment`：先全子树验证、后统一提交）也忠实对齐了
`type_set_bases_unlocked` 的两阶段语义——失败时图不被触碰。

代价与风险：

- **组合空间被继承链焊死**。CPython 的 `tp_dictoffset`、`tp_flags` 是彼此独立的
  自由度；CLR 单继承链只能表达一条轴。「实例可变但无实例字典」这类组合在
  CPython 中合法（`__slots__` 类的实例是可变对象），PySharp 只能用
  `IsImmutable => true` 的哨兵类表达，语义被压扁（见下一节「概念重载」）。
- **哨兵类型是逻辑标签而非物理布局**。`PySlotsObject` 并没有真正的槽位数组：
  `__slots__` 成员的读写经 `CreateSlotDescriptor` 落到
  `PyAttachedPropertiesManager` 的条件弱表（`PyTypeObject.Default.cs` 的
  `Set_Slot`）。也就是说，`__slots__` 的存储后端与普通附加字典相同，而后者
  （`PyObjectManagedDict`）反而是更便宜的实例字段。「省字典开销」这一
  `__slots__` 的核心收益在当前实现中是**负收益**：既付出了弱表查找，又保留了
  条目字典。行为面（无 `__dict__` 暴露、未声明名拒写）完全正确，但性能语义
  与 CPython 方向相反。若未来做实例存储分化，这是明确的优化入口；若不做，
  建议在性能文档中如实记录该差异，避免使用者基于 CPython 经验做性能决策。

## 标志的 OOP 化

CPython 的 `tp_flags` 是一张位图；PySharp 把它拆成两部分：

- `PyTypeFlags`（`Sequence` / `Mapping` / `MatchSelf`）：只建模模式匹配需要的位，
  构造期查静态表 + 沿 MRO 继承（对齐 `inherit_patma_flags`）。窄而克制，合理。
- 其余语义各自成为一个布尔虚属性：`IsRuntimeCreated`、`IsSealed`
  （`Py_TPFLAGS_BASETYPE` 的反面）、`InstancesAreImmutable`、
  `InstancesCarryInstanceDict`、`ReleasesWithFreeList`。

布尔虚属性的读法比位运算清晰，但有一个**概念重载**值得警惕：
`InstancesAreImmutable` 的文档注释说它建模「`tp_dictoffset == 0`」，名字却暗示
「值不可变」。对 int/str/tuple 两者恰好同真；对 `__slots__` 类实例，CPython 语义
是「可变对象、无实例字典」，PySharp 通过 `PySlotsObject.IsImmutable => true`
让 `InstancesCarryInstanceDict => !InstancesAreImmutable` 的推导继续成立——正确
性无虞，但任何未来消费者若把 `IsImmutable` 当作「值是否可变」（例如拷贝协议、
可哈希性推断）就会拿错信号。两个概念（无字典 / 值不可变）目前恰好同沿线，
建议要么改名（如 `InstancesCarryNoDict`），要么在属性注释里显式声明
「本属性不表达值可变性」。

## 实例属性的双轨存储

`PyObject.PyAttributes` 是 internal virtual 接口属性，两个实现：

- `ConditionalWeakTable`（`PyAttachedPropertiesManager`）：默认路径，
  惰性分配 `PyId` 与条目字典。弱表保证对象生命周期语义与 CPython 引用计数
  一致（字典随对象消失）。`GetId` 的双检锁 + `Interlocked` 组合正确。
- `PyObjectManagedDict._pyAttributes` 实例字段：高频带字典类型
  （模块、异常、用户类实例）的快路径，接口同一。

双轨并存是合理的性能分层，且接口（`IPyAttributesObject`）把边界固定得很窄。
风险点在于**第三轨**：`__slots__` 成员也走弱表（见上节），于是同一个弱表里
同时装着「实例字典」与「槽位成员」两种逻辑数据。当前以 descriptor 种类
区分访问路径，没有串扰，但弱表内数据的语义多样性增加了排查成本。

## MRO 与子类注册

C3 线性化的实现（`TryCreateMROWithoutSelf`）逐条对齐了 `mro_implementation`，
包括 `__bases__` 重赋值时用 `overriddenMros` 注入「尚未提交的祖先线性化」，
以及 `set_mro_error` 报 stuck heads 的错误拼写。C# 实现里用
`List<Queue<PyTypeObject>>` 模拟 C 的链表数组，`tail.Contains(head)` 的线性扫描
在大 MRO 下是 O(n²)，但 n 是 MRO 长度（个位数到十几），不是实例操作热路径，
可接受。

子类注册（`RegisterSubclass` / `EnumerateLiveSubclasses`）只在运行时创建的类型上
登记、静态类型不登记（否则 object 的注册表将是几百个启动期条目的死重），
并带「注册/枚举时顺带修剪已回收项」的策略——这两条决策都有源码注释论证，
且经得起推敲。

## 小结

对象/类型模型层的总评：**映射忠实、分层清楚，是 PySharp 质量最高的层之一**。
布局借道 CLR 类型系统与命名面溯源是两个突出的优点。需要跟进的三件事：

1. `__slots__` 存储后端与布局标签的背离（性能语义倒挂）——至少先文档化；
2. `IsImmutable` 的概念重载——改名或补注释，防止未来误用；
3. `__bases__` 重赋值路径上槽缓存与类型字典的失同步——这是正确性缺陷，
   归入槽系统篇（[02-slots-and-dispatch.md](./02-slots-and-dispatch.md)）详述。
