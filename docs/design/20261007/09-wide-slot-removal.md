# 宽槽位移除：回归调用点查名

前作与动机：槽位分层迁移完成后，[05 篇](./05-recent-tradeoffs.md)的差分评审
（登记于 [protocol-dispatch.md](../../reference/internals/protocol-dispatch.md) 的
"宽槽位已知差异"节，提交 1821ab05）确认了唯一一处可观测偏差：十三个 CPython 无
slotdef 的 dunder 被提升为槽字段后，绑定发生在接线期（`TrySetSlot` 把赋入值统一
转换为携带 self 的槽 delegate），而 CPython 的绑定发生在查名期（描述符协议）。
非描述符可调用对象赋给宽槽 dunder 时两侧调用形态分歧。本篇记录消除该偏差的
重构决策与验证。

## 决策

1. **删除槽字段，回归查名**：十三个宽 dunder 从 `PyTypeObject.Declarations.cs`
   摘除（槽字段、`TrySetSlot`/`ClearSlot`/`TrySetWrappedSlot`/`FillNullWith`/
   `AllSlotNames` 条目随之消失），类型字典成为唯一事实源。消费点（`format()`/
   `round()`/`math.floor` 等、`reversed()`、dict 下标、`type` 创建的
   `__set_name__` 循环、VM 的 `LOAD_SPECIAL`、`complex()`）统一经新原语
   `PyUtils.TryLookupSpecial`（`_PyType_Lookup` + 描述符绑定，非描述符原样）
   解析。运行期增删即纯字典操作，`UpdateSlot` 的门控自然不再覆盖这些名字。
2. **complex 死槽顺带补齐**：`Slots.Complex` 原本只写不读（`complex()` 不分发
   `__complex__`）。随本次删除并按 CPython `complex_new_impl` 补齐：
   `try_complex_special_method` 只作用于 "real" 侧（转换结果**替换** r，
   `cr_is_complex` 按转换后判定——虚部参与 deprecated 组合运算、弃用警告按
   原始参数的 `__float__`/`__index__` 协议有无决定），"imag" 侧直接走
   `__float__` → `__index__`，返回值校验对齐（`__complex__ returned
   non-complex`）。
3. **round 的 0/1 参分派**：实测 3.14 定调 `round(x)` 对 `__round__` 是无参
   调用（单参 `def __round__(self)` 在 `round(x, n)` 下报
   `takes 1 positional argument but 2 were given`）。内建类型的二元 Round 桥
   经 `FillRoundDictView` 包成 `PySelfArgsKwargsFunction` 形态的 0/1 参自适应
   字典视图；`PySpecialMethods.Round` 按 ndigits 是否为 None 分派实参数。
4. **with 的 LOAD_SPECIAL 绑定化**：CPython 3.14 的 `LOAD_SPECIAL` =
   `_INSERT_NULL + _LOAD_SPECIAL`（method/self 双槽协议，查名后原地替换 method
   槽，描述符绑定与非描述符均清空 self 槽）。PySharp 的 VM 侧改为弹出管理器、
   查名绑定后压入已绑定可调用；发射端采用 `Copy 1; LoadSpecial Exit; Swap 2;
   LoadSpecial Enter; Call 0` 的单交换形状（CPython 的 `SWAP 2; SWAP 3` 双交换
   是其双槽布局的产物，PySharp 布局下一次即可），enter `CALL 0`、exit 异常
   三元组 `CALL 3`，与 CPython 3.14 的 `dis` 序列逐指令核对。
5. **object 的 `__format__` 字典条目**：对齐 CPython 的 `object.__dict__`——
   `PyObjectType` 构造时安装 `DefaultFormat` 的 wrapper descriptor。查名从此
   必中（`del C.__format__` 回落 object 默认），`PySpecialMethods.Format` 的
   `?? DefaultFormat` 兜底退役，`getattr(obj, "__format__")` 与 `dir` 可见性
   随之对齐。
6. **内建覆写走字典视图通道**：生成器新增 `WideDictSlotNames` 名单，覆写检测
   到宽 dunder 虚方法时只发 `FillWideDictView`（字典 wrapper，与 R* 反射视图
   同通道）。虚方法签名与 `[PySlot]` 标记保留：消费者程序集的覆写兼容，且
   继承检测（`inherit: true` 的符号查找）继续服务库外类型。`Slots.Get` 绑定、
   `FillSlot` 的字典条目均不感知此变化。
7. **既有内联范本不收敛**：`PyUtils.LengthHint`（吞 AttributeError 回落）、
   dir 的 `__dir__`、`CallTypeCheckHook` 三处已有的查名+绑定内联保持原样——
   LengthHint 的错误吞咽语义与原语不同，收敛会引入行为变化，留给后续卫生项。

## 实施中暴露的连带面

- **with 的 return/break 内联展开**：`EmitUnwindRegion` 的 WithItem 分支按旧
  三元素驻留栈 `[exit, manager, value]` 写的 `Swap 3; Swap 2` 在新布局
  `[exit, value]` 下损坏栈序（return 穿过 with 时 exit 收到错误实参甚至不被
  调用），改为单次 `Swap 2`；`EmitWith` 进入序列同样补了 `Copy 1` 后的
  `Swap 2`（第二个 `LOAD_SPECIAL` 之前，bound exit 必须沉底）。
- **None 实例与 wrapper 描述符的哨兵冲突**：`PyWrapperDescriptorObject.Get`
  用 `instance is PyNoneObject` 充当 CPython 的 `obj == NULL`（类访问不绑定）。
  object 的 `__format__` 字典条目落地后，`format(None)` 的查名绑定把真实的
  None 实例当成了类访问，拿回未绑定 wrapper 后以缺参 TypeError 失败。
  `PyUtils.TryLookupSpecial` 对 None 实例直连构造绑定（`PyMethodWrapperObject`）
  绕开哨兵；「Get 协议缺真正的 NULL 哨兵」登记为后续卫生项。
- **复杂化的复杂组合**：见决策 2——`cr_is_complex` 按转换后判定，imag 侧不
  查 `__complex__`，警告条件对齐 CPython 的 nbr 检查（原参数有无
  `__float__`/`__index__` 协议）。

## 登记的后续

- **查名无缓存**：`TryLookupAttrInMro` 是 MRO 线性扫描，`format`/with 热路径
  由槽字段直读改为查名。对齐优先，MRO 查名缓存（对应 CPython 的
  method cache / tp_version_tag 失效）登记为 perf 后续。
- **内建方法呈现差异**（既有现状，非本次引入）：`object.__format__` 在
  PySharp 显示 `wrapper_descriptor`，CPython 是 `method '__format__' of
  'object' objects`——内建方法描述符体系的历史差异，与本机制无关。

## 验证

- 13 组手工差分探针（绑定形态、None 语义、动态增删、round/complex/math 消息）
  逐项与 CPython 3.14.4 对齐。
- 新增差分语料：`test_wide_slot_binding.py`（非描述符/staticmethod/None 赋值
  的调用形态，with/async-with/reversed/missing/round）、
  `test_wide_slot_dynamic.py`（运行期增删回落、object 字典可见性、
  `__set_name__` 时机）、`test_complex_conversion_protocol.py`（complex 转换
  序与校验）。
- 全量回归（进程内 + CPython 差分双引擎，489+ 语料）通过；构建零警告。
