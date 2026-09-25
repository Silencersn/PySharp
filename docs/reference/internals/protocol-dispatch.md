# 协议分发

源码：`PySharp/Runtime/PySpecialMethods.cs`、`PySharp/Runtime/Comparison/`。

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
- 哈希归一化：`Hash` 把 `__hash__` 的结果经 `PyHash.HashLong` 归一化，保证实现内不变量（如相等
  对象哈希相等），无槽类型直接判不可哈希。

## 运算符的反射协议

二元运算入口 `ReflectiveOperator` 对齐 CPython 的 `binary_op1`，按三档决策：

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
- 比较族（`Lt` 与 `Gt` 族）：无条件以「右操作数的镜像形式优先」调用，即 `left < right` 先试
  `right.__gt__` 再试 `left.__lt__`，与算术不同，不受同类型省略的影响。`Eq` 是特例，反射查询
  两侧都查 `__eq__`，均返回 `NotImplemented` 时退化为引用相等。
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

## 比较器

`Runtime/Comparison/` 下的类型：

| 类 | 职责 |
| --- | --- |
| `PyComparer` | `Eq`、`NotEq`、`Lt`、`LtE`、`Gt`、`GtE` 的统一入口，把委托结果转 `PyBoolObject`；容器排序（`sorted`、`list.sort`）复用 |
| `PyCollectionComparer` | 集合与序列的逐元素比较，即字典序语义 |
| `PyObjectComparer` | 与 `PyCallContext` 绑定的比较器对象（`context.Comparer`） |
| `PyObjectConstEqualityComparer` | 常量池去重用的 `IEqualityComparer<PyObject>`，按 Python 的 `==` 与 `hash` 语义，服务 `BytecodeBuilder` 的常量池 |

## 修改协议行为的位置

- 给内建类型加协议：在元类型上覆写 `protected virtual` 方法，或 `FillSlot`，见
  [对象模型](./object-model.md)。
- 用户定义类的 `__repr__` 等在类体中定义，由类型创建路径把它们登记进类型属性与槽。
- 新增全局协议入口：在 `PySpecialMethods` 加分发方法并加对应槽字段，注意与 CPython 的回退行为
  对齐，并补 `test_pyfiles` 回归。
