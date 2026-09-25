# 对象模型

源码：`PySharp/Modules/Builtins/PyTypeObject*.cs`、`PyObject.cs`、
`PyAttachedPropertiesManager.cs`。

PySharp 的对象模型贴近 CPython 的数据模型：一切皆 `PyObject`（含类型对象自身），类型行为由类型
对象上的槽（slots）与属性字典共同决定，方法解析沿 MRO 进行。

## 类型对与 partial 拆分

每个内建类型由配对加多个 partial 组成。以 `str` 为例，`PyStrObject` 主文件加 `Intern`、`Py`、
`Converter` 分部，`PyStrObjectType` 的注册代码由源生成器产出 `.g.cs`：

| 值类型职责 | 元类型职责 |
| --- | --- |
| 字段承载数据，如 `PyStrObject.Value` | `[PyType("str")]` 标注与 `Shared` 单例 |
| `DefaultPyType` 指回元类型 | 协议实现：虚方法覆写与 `FillSlot` 填槽 |
| Python 侧方法在 `.Py.cs` 分部 | 方法与属性描述符注册，生成器调用 `AppendMethodDescriptor` 等 |

`PyTypeObject` 与 `PyTypeObject<T>` 的 partial 拆分：

| 文件 | 内容 |
| --- | --- |
| `PyTypeObject.cs` | 身份：`Name`、`QualName`、`Module`、`FullName`、`Bases`、`MRO`（`InternalMRO` 为 span 版）、`IsInstance`、`IsSubclassOf`、`LayoutType` |
| `PyTypeObject.Slots.cs` | `protected internal PyTypeSlots Slots`：协议委托槽（`New`、`Number` 复合块与各协议字段）及跨基类槽合并 |
| `PyTypeObject.Default.cs` | 协议缺省实现，如 `DefaultGetAttribute`（MRO 加描述符加 `__dict__`）、`DefaultStr`、`DefaultRepr`、`DefaultEq` |
| `PyTypeObject.Declarations.cs` | 声明性元数据，供生成器与分析器读取 |
| `PyTypeObject.Virtual.cs` | 非泛型侧虚方法 |
| `PyTypeObjectOfT.cs`、`.Init.cs`、`.Virtual.cs` | 泛型侧：构造流程（`FillSlots()` 虚钩子）、成员注册 API（`AppendMethodDescriptor`、`AppendClassMethod`、`AppendStaticMethod`、`AppendMemberDescriptor`，由生成代码调用）、92 个 `protected virtual` 协议方法 |

`PyTypeSlots` 的字段即协议槽，例如 `Repr`、`Str`、`Len`、`Hash`、`Iter`、`Next`、`GetItem`、
`SetItem`、`Contains`、`Call`、`Eq`、`Add`、`RAdd`、`IAdd`。完整清单见 `PySpecialMethods` 与
`PyOperators` 的分发面，即[协议分发](./protocol-dispatch.md)。槽类型是 `PyDelegates.cs` 中的
委托族：

```csharp
public delegate PyResult PyFunction(PyCallContext context, PyArguments arguments);
public delegate PyResult PyMethod<TObject>(PyCallContext context, TObject self, PyArguments arguments) where TObject : PyObject;
public delegate PyResult PyBinaryFunction(PyCallContext context, PyObject self, PyObject other);
// 另有 PyUnaryFunction、PyTernaryFunction、PySelfArgsKwargsFunction、PyClsArgsKwargsFunction、PyMemberGetter/Setter/Deleter 等
```

`PyDelegateDefinition<T>` 与 `PyDelegateConverter.CreateOverloadDispatcher` 把多个同名签名合并
为按实参分派的重载分发器，例如 `math.log` 的单参与双参版本。

## 属性与实例字典

- `PyObject.PyAttributes` 为 `internal virtual`，类型是内部接口 `IPyAttributesObject`（含
  `TryGetValue`、set 索引器、`ContainsKey`、`Remove` 与枚举）。默认经
  `PyAttachedPropertiesManager.Shared` 的条件弱表（`ConditionalWeakTable`）支撑，`GetId` 分配
  稳定的 `PyId`，`GetDict` 与 `SetDict` 管理附加字典。不可变类型返回共享的冻结空实现
  `IPyAttributesObject.FrozenEmpty` 并拒绝写入。
- `PyObjectManagedDict`：带真实每实例字典的类型（模块、异常、用户类实例）改用实例字段
  `_pyAttributes`，同一接口，惰性创建为一个 `PyDictObject`，避免弱表开销。
- 惰性属性模式：能从自身状态直接算出的属性不在构造时写入字典，而是用 `[PyProperty]` getter 按需
  导出。函数的 `__name__` 与代码对象的 `co_*` 系列都如此，`__name__` 另有 setter 支持运行时
  改名。
- 类型对象的属性即「类属性」：方法描述符、成员描述符、`__init_subclass__` 等存放于此。实例查找走
  `DefaultGetAttribute`，顺序为数据描述符、实例 `__dict__`、非数据描述符与类属性（按 MRO），
  最后回退 `__getattr__`，由 `PyOperators.GetAttr` 两段式编排。

## MRO 与子类化

- `MRO` 在类型构造时按 C3 线性化语义计算，`TryLookupAttrInMro` 沿 MRO 查找属性。
- 用户定义类（`class` 语句）由 `_BuildClass` 与 `PyCore.BuildClass` 依据基类集合与元类创建。
  `LayoutType` 机制保证继承内建类型时实例布局兼容（`CreateUserDefinedTypeWithSameLayout`），
  用户类实例为 `PyObjectManagedDict` 布局。
- 元类：最派生元类的决议在 `BuildClass` 中完成。`metaclass` 关键字参数需为 `PyTypeObject`。

## 描述符协议

三种描述符对象支撑属性机制：`PyMemberDescriptorObject`（成员 get、set、del）、
`PyMethodDescriptorObject`（把非托管到托管的签名包装为方法绑定）、`PyWrapperDescriptorObject`
（把槽包装成可调用对象，如 `with` 语句经 `LoadSpecial` 取出的 `__enter__` 与 `__exit__`）。
`PyPropertyObject`、`PyStaticMethodObject`、`PyClassMethodObject` 与 `PySuperObject` 在此基础上
实现 `property`、`staticmethod`、`classmethod` 与 `super`。

## 类型机器的生成

上述注册样板（构造 `Shared`、调用
`AppendMethodDescriptor("join", new PyDelegateDefinition<...>(Join, ["iterable", "/"]))`、
覆写 `FillSlots` 等）由源生成器按 `[PyType]` 与 `[PyMethod]` 等特性生成到
`obj/generated/.../*.g.cs`，运行时零反射，这是 AOT 兼容的关键。见
[源生成器与代码分析](./source-generators.md)，扩展模式见
[用 C# 定义 Python 类型](../user-guide/custom-types.md)。

## 单类型深潜

- [字符串系统](./string-system.md)：`PyStrObject` 的驻留体系、字面量转换、方法注册
- [数值系统](./numeric-system.md)：`PyIntObject` 缓存、`PyMath` 整数快速路径、`PyHash` 归一化
- [dict 系统](./dict-system.md)：自研哈希桶、属性与 locals 字典接口分层、帧 locals 代理
- [生成器与协程系统](./generator-system.md)：挂起与恢复状态的承载与三种身份
- [用户类创建全流程](./class-creation.md)：`class` 语句从发射到 `type.__new__` 的链路
