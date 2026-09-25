# dict 系统

源码：`PySharp/Modules/Builtins/PyDictObject.cs`（数据结构，512 行）、`PyDictObject.Type.cs`
（`PyDictObjectType`，246 行）、`PyDictObject.StringKeyInterface.cs`（接口实现分部，38 行）、
`PyDictItemsObject.cs`（视图与迭代器，504 行）、`PyFrameLocalsProxyObject.cs`（帧 locals 代理）、
`PySharp/Runtime/PyVariables.cs`（`IPyVariablesLocalsDict`）、
`PySharp/Modules/IPyObjectName.cs`（`IPyAttributesObject`）。

dict 是解释器中使用最多的容器：模块与类的属性字典、帧的 globals 与 locals、`exec` 与 `eval` 的
名字空间、`**kwargs` 的收集。它已从「包一层 .NET 字典」重写为自研哈希桶，并与解释器其余部分保持
同一风格：不依赖 .NET 泛型字典、插入顺序稳定、AOT 友好。本篇覆盖三块：底层数据结构、围绕它的两个
internal 字典接口（属性与 locals），以及帧变量的 locals 代理。

## 数据结构

```csharp
public partial class PyDictObject : PyObject, IPyObjectRecursiveRepr
{
    private int _count;
    private int[] _buckets;      // 桶数组：存 entryIndex + 1，0 表示空桶
    private Entry[] _entries;    // 条目数组：[0.._count) 有效，天然保持插入顺序

    internal struct Entry
    {
        public int Next;         // 链表下一个条目下标，-1 表示链尾
        public uint HashCode;    // 缓存的键哈希
        public PyObject Key;
        public PyObject Value;
    }
}
```

要点：

- 分离式链址：`_buckets[hash % buckets.Length]` 存「条目下标加一」，0 留作空桶哨兵，冲突条目经
  `Entry.Next` 串成链，`PushEntryIntoBucket` 把新条目头插进桶链。
- 插入序即枚举序：条目只追加在 `_entries[_count]`，`Entries` span 就是 CPython 语义的迭代顺序。
  `PopItem` 弹出最后一个条目，与 CPython 一致。
- 容量走素数表：`EnsureCapacity` 调 `Helper.GetPrime`（与 .NET `Dictionary` 同源的素数表），
  `Resize` 时复制条目并全量重挂桶。
- 删除是 O(n)：条目从 `_entries` 中移除后，其后的条目整体左移补洞，然后清空并重建全部桶，
  源码中标注 `TODO: perf`。这是当前实现已知的性能取舍，读与插入路径优先。

### 删除的统一编码

`InternalSetItem(context, key, value)` 把赋值、删除与取走统一在一个入口：`value` 非 `null` 为赋值
（命中则原地覆盖）；`value` 为 `null` 即删除，命中时返回旧值，未命中返回 `KeyError`。`DelItem`
与 `Pop` 都走这一约定，只是对返回值的包装不同。

## 两条访问路径

### string 键快速路径

```csharp
public bool TryGetValue(string key, out PyObject? value);
public PyResult GetItem(string key);
public void SetItem(string key, PyObject value);
public bool ContainsKey(string key);
public bool DelItem(string key);
public PyObject this[string key] { get; set; }   // 读取未命中抛 KeyNotFoundException
```

这是 C# 侧（宿主、扩展实现与运行时内部）最常用的通道：直接以 .NET 字符串哈希
（`PyStrObject.GetHashCode(key)`）定位桶，命中后按 `string.Equals(..., Ordinal)` 比较。它不分配
中间 `PyStrObject`、不调用 `PyComparer`、不需要 `PyCallContext`。模块属性与 globals 查名等热路径
全部受益。

### 协议路径

任意 Python 对象做键时走 `GetItem`、`SetItem`、`DelItem` 的 `PyCallContext` 重载：先经
`PySpecialMethods.Hash` 取键哈希并缓存进条目，桶内哈希命中后再 `PyComparer.Eq` 判等。即键相等由
类型的 `__eq__` 与 `__hash__` 决定，与 CPython 语义一致。

两路能共存于同一张表，是因为 `str` 的 Python 哈希就定义为 `.GetHashCode()`（`-1` 映射 `-2`，
见[数值系统](./numeric-system.md)），同一字符串经快速路径与协议路径得到同一桶位。

`PyDictObject` 不实现 `IDictionary` 族接口。对 C# 侧暴露的面就是上面的 string 键方法、协议重载
与 `Count`、`GetEnumerator()`（按插入序枚举键值对）。宿主侧用法见
[内建类型速查表](../api/builtin-types.md)。

## PyDictObjectType

元类型的槽位与 `dict` 方法集中在 `PyDictObject.Type.cs`，值类型主文件不再掺杂类型定义：

- 构造的 `__new__` 与 `__init__` 拆分，对齐 CPython 的 `dict_new` 与 `dict_init`：`New` 只分配
  空 dict 并忽略全部参数（含回写 `obj._pyType = cls` 使子类保持身份）；消费可迭代参数与关键字
  参数的初始化在 `Init`，即至多一个位置源（经 `Update` 归化）加关键字逐项写入，超出时报
  `TypeError: expected at most 1 argument`。这样 Python 子类的自定义 `__init__` 会取代内置
  初始化并拿到全部构造参数。直接调用 `d.__init__(...)` 的再初始化语义是合并更新，不清空。
- `__missing__` 钩子：`GetItem` 槽在 `KeyError` 时查 `Slots.Missing`，dict 子类实现
  `__missing__` 即可定制缺键行为。
- 方法面：`items`、`keys`、`values`、`clear`、`get`、`setdefault`、`pop`（单参与双参两个重载）、
  `popitem`、`copy`、`update`、`fromkeys`（classmethod）。`Eq` 槽逐条目经 `PyComparer.Eq` 比较，
  `Bool`、`Len`、`Contains`（经 `IsKeyError` 判定）、`Iter`（迭代键）。
- `builtins` 模块经 `[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyDictObjectType))]`
  把名字 `dict` 绑到类型单例，因此 `dict(...)` 即调用类型本身，走 `New` 槽。

## 接口分层

dict 的三个消费场景（对象属性、帧 locals、全局名字空间）对字典的要求不同，用两个 internal 接口把
边界固定下来，`PyDictObject` 同时实现两者，显式实现集中在 `PyDictObject.StringKeyInterface.cs`：

| 接口 | 定义处 | 语义侧重 |
| --- | --- | --- |
| `IPyAttributesObject` | `Modules/IPyObjectName.cs` | 属性字典：`TryGetValue`、set-only 索引器、`ContainsKey`、`Remove`、枚举、`Self`；附带共享的 `FrozenEmpty` 冻结空实现 |
| `IPyVariablesLocalsDict` | `Runtime/PyVariables.cs` | locals 字典：可空的 `PyObject?` 索引器与 `TryGetValue`、`Remove`、`ContainsKey`、枚举。可空性对应「槽位存在但未绑定」 |

设计动机：属性字典只需要写入与查找（值永不为 `null`，删除走 `Remove`）；locals 必须区分「名字在
槽表里但值未绑定」，因此读取类型可空，对应 `UnboundLocalError` 语义。两者都比
`IDictionary<,>` 更窄，`PyDictObject` 用显式接口实现分别适配。

属性系统的接线，详见[对象模型](./object-model.md)：

- `PyObject.PyAttributes` 的类型即 `IPyAttributesObject`。普通对象经
  `PyAttachedPropertiesManager` 的条件弱表惰性挂接；不可变类型返回 `FrozenEmpty`，写入抛
  `NotSupportedException`。
- `PyObjectManagedDict`（模块、异常、用户类实例）持有 `_pyAttributes` 字段，首次访问才创建
  `PyDictObject`。
- `PyModuleObject.PyAttributesDict` 是内部取「真身」`PyDictObject` 的捷径。

## 帧变量与 locals 代理

`PyVariables`（`Runtime/PyVariables.cs`）的字典分层：

- `_globals`：恒为 `PyDictObject`。模块帧的 globals 就是 `__dict__` 本体，`globals()` 返回这个
  活字典，对它的修改直接改模块名字空间。
- `_locals` 有三种形态：`null`（模块与类体以外的无 locals 帧，`LoadName` 与 `StoreName` 直落
  globals）、快槽（`_localsTable` 加 localsPlus 内存，普通函数帧按下标存取，无字典参与）、
  `IPyVariablesLocalsDict` 慢路径（实现只有 `PyDictObject` 与该代理）。

### PyFrameLocalsProxyObject

`exec`、`eval` 与推导式内联帧需要「既看到外围快槽、又能承载运行期新增名字」，
`PyFrameLocalsProxyObject`（internal，Python 侧类型名 `FrameLocalsProxy`，可 `repr`）就是这两层
的桥：

- 构造入参为 `FrozenDictionary<string, int> localsTable`（名字到槽位）、
  `Memory<PyObject?> localsPlusMemory`（槽存储，与帧共享内存，写入直达）与可选的 `_extraLocals`
  （`PyDictObject`，惰性创建，收纳槽表之外的新名字）。
- 读取先查槽表，未命中查 `_extraLocals`；写入在槽表命中时写槽，否则进 `_extraLocals`；
  `Remove` 将槽置 `null` 或删除溢出字典条目。
- `exec` 与 `eval`（`CreateExecEvalFrame`）传入的 locals 是 `PyDictObject` 时直接作为接口实现
  使用，写入直达调用者的字典，与 CPython 一致；仅当带闭包（自由变量）时才包装代理。

### locals() 的快照语义

`locals()`（`PyBuiltinFunctions.LocalsImpl` 调 `Variables.GetLocals`）每次返回新快照：

- 快槽形态：遍历 `_localsTable`，解包 `PyCellObject`（闭包变量取单元内容），跳过未绑定槽，
  组装成新的 `PyDictObject`。
- dict 与代理形态：同样解包单元后逐对拷贝。

这与 CPython 语义一致，函数帧内 `locals()` 的修改不影响真实局部变量。而模块帧的 `GetLocals`
直接返回 globals 本体，即活字典。

### del 与 DeleteGlobal

`del x` 的落地：函数帧内走 `DeleteLocal`（槽置 `null`，溢出名走 `Remove`，都不可用时报
`UnboundLocalError`）；模块级走 `DeleteName` 到 `DeleteGlobal`（`Globals.DelItem(name)`，未命中
报 `NameError`）。

## 视图与迭代器

`PyDictItemsObject.cs` 用一个视图类加一个迭代器类覆盖三套类型：

- 视图：`PyDictItemsObject` 持有对 dict 的引用，按工厂区分为 `dict_keys`、`dict_values`、
  `dict_items` 三个元类型。
- 迭代器：`PyDictItemIteratorObject` 同样一族三型（`dict_keyiterator`、`dict_valueiterator`、
  `dict_itemiterator`），共享一个 `Next()`，按 `DefaultPyType` 决定产出键、值或 `(key, value)`
  元组。
- 反向迭代：dict 本体的 `Reversed` 槽产出反向键迭代器；三视图各有 `Reversed`，复用同一
  `PyDictItemIteratorObject` 的 `reverse` 模式，类型名对齐 CPython（`dict_reversekeyiterator`、
  `dict_reversevalueiterator`、`dict_reverseitemiterator`）。`reversed(d)` 因此不走 len 与
  getitem 的序列回退，那会在非连续键上报 `KeyError`。变更检测与「已耗尽」「已失效」的下标编码与
  正向一致。
- 尺寸变更检测：迭代器创建时快照 `_count`，每次 `Next` 对照当前 `dict.Count`，不一致即报
  `RuntimeError("dictionary changed size during iteration")`。

## 性能与并发

- 读与插入是摊还 O(1)；删除与 `PopItem` 触发 O(n) 的条目搬移与桶重建，源码标注 `TODO: perf`，
  高频删除场景是明确的优化方向。
- 哈希一致性由 `PyStrObject.GetHashCode`（`-1` 映射 `-2`）与 `PySpecialMethods.Hash` 到
  `PyHash.HashLong`（模 2⁶¹−1 归一化）保证，见[数值系统](./numeric-system.md)。
- 非线程安全：纯数组结构无锁。没有 GIL，见 [threading 与 queue](./threading-and-queue.md)，
  跨线程共享 dict 需外部同步。

## 相关阅读

[对象模型](./object-model.md)（属性系统全景）· [调用与帧](./calls-and-frames.md)（PyVariables
与帧形态）· [数值系统](./numeric-system.md)（哈希归一化）· [虚拟机](./virtual-machine.md)
（`__missing__` 槽分发）
