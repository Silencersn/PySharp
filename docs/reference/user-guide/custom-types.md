# 用 C# 定义 Python 类型

范例源码：`PySharp/Modules/Queue/PyQueueObject.cs`（类型对）、
`PySharp/Modules/Builtins/PyObject.cs`（槽填充）。总览见
[扩展 PySharp](./extending-pysharp.md)。

PySharp 的每个内建类型都是一个类型对：值类型 `Py*Object : PyObject` 承载数据，元类型
`Py*ObjectType : PyTypeObject<T>` 定义 Python 行为（方法、属性、协议槽）。定义自己的类型时沿用
同样的结构，由 Roslyn 源生成器（`PySharp.SourceGeneration`）根据特性生成注册代码，运行时不需要反射。

这套模型是解释器定义全部内建类型的方式。方法、属性与协议覆写在库外程序集同样可用：协议槽的
填充代码由生成器产出，库外的类型对走同一条路径。仍属 `internal` 的接线点只有一处——`New`
覆写中为支持 Python 子类化写 `obj.Value._pyType = cls`（见[定义构造行为](#定义构造行为)）。
新类型对 Python 代码的可见性还需要挂载到模块上，见文末与
[用 C# 编写 Python 模块](./custom-modules.md)。

## 类型对骨架

以 `queue.Queue` 的实现（节选）为例：

```csharp
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

// 1. 值类型：承载数据
public sealed partial class PyQueueObject : PyObject
{
    private readonly ConcurrentQueue<PyObject> _queue;
    // ... 其他字段

    public override PyTypeObject DefaultPyType => PyQueueObjectType.Shared;
}

// 2. 元类型：定义 Python 行为
[PyType("Queue", Module = "queue")]
public sealed partial class PyQueueObjectType : PyTypeObject<PyQueueObject>
{
    [PyMethod("qsize")]
    [PyFunctionParameters()]
    private static PyResult QSize(PyCallContext context, PyQueueObject self, PyArguments arguments)
        => PyIntObject.FromInteger(self.PyQSize());
}
```

要点：

- `[PyType("Queue", Module = "queue")]` 声明 Python 侧的名字，即 `queue.Queue`。`Module` 默认
  `"builtins"`，`IsSealed` 默认 `false`。
- `DefaultPyType` 返回元类型单例，构造实例时由运行时写入 `PyType`。
- 两个类都声明 `partial`。生成器会补齐类型机器：`Shared` 单例、私有构造函数、
  `DefaultName`、`DefaultModule`、`IsSealed`、`FillSlots()` 与 `RegisterMethods()`、
  `RegisterProperties()` 都由 `.g.cs` 产出，手写这些成员会与生成代码冲突。仅当标注
  `[PyTypeConstructor(DoNotGenerateConstructor = true)]` 时才需要自己写构造与单例，如
  `PyObjectType`。
- 基类构造在注册前后各留一个覆写点：`PreConstruct()` 在 `FillSlots()`、`RegisterMethods()` 与
  `RegisterProperties()` 之前调用（此时身份、MRO 与类型字典已就绪）；`PostConstruct()` 在全部
  注册完成后调用。需要定制类型字典（如替换 `__setattr__` 的包装描述符、把 `__hash__` 置
  `None`）时覆写 `PostConstruct()`，而不是用静态构造函数在初始化后再改 `Shared`。

## 定义方法

静态方法加两个特性，签名固定为 `(PyCallContext context, T self, PyArguments arguments)`：

```csharp
[PyMethod("put")]                          // Python 方法名
[PyFunctionParameters("item", "block=True", "timeout=None")]   // 形参签名，用于参数校验
private static PyResult Put(PyCallContext context, PyQueueObject self, PyArguments arguments)
{
    var item = arguments[0];
    // ... 实现
    return PyNoneObject.None;
}
```

- `PyArguments` 按声明顺序取参数，即 `arguments[0]`。缺参、多参与未知关键字会按
  `[PyFunctionParameters]` 的声明校验并给出 Python 风格的 `TypeError`。签名串支持默认值、
  `/`（仅位置）、`*`、`**kwargs` 等 Python 记法。
- 返回 `PyResult`，成功值为对象本身，失败用 `PyResult.TypeError(...)` 等错误工厂，见
  [运算符与协议分发](./operators-and-protocols.md)。
- 变体：`[PyStaticMethod("name")]` 定义静态方法并省略 `self` 参数；
  `[PyClassMethod("name")]` 定义类方法，首参为类型对象。

## 定义属性

成对的 getter、setter、deleter 用 `PyPropertyMethodType` 区分：

```csharp
[PyProperty("size")]
private static PyResult Get_Size(PyCallContext context, PyQueueObject self)
    => PyIntObject.FromInteger(self.PyQSize());

[PyProperty("size", Type = PyPropertyMethodType.Setter)]
private static PyResult Set_Size(PyCallContext context, PyQueueObject self, PyObject value)
{
    // ... 赋值逻辑；需要拒绝时返回 PyResult.AttributeError(...)
    return PyNoneObject.None;
}
```

## 实现协议

两种方式可混用。

方式一，覆写虚方法。`PyTypeObject<T>` 为每个协议暴露 `protected virtual` 方法，直接 override：

```csharp
protected override PyResult Repr(PyCallContext context, PyQueueObject self)
    => PyStrObject.FromString($"<Queue size={self.PyQSize()}>");

protected override PyResult GetItem(PyCallContext context, PyQueueObject self, PyObject key)
    => /* ... */;
```

可覆写的项有 90 余个，覆盖 `New`、`Init`、`Call`、`Repr`、`Str`、`Hash`、`Bool`、`Int`、
`Float`、`Complex`、`Index`、`Len`、`Iter`、`Next`、`GetAttribute`、`GetAttr`、`SetAttr`、
`DelAttr`、`GetItem`、`SetItem`、`DelItem`、`Contains`、`Missing`、`Abs`、`Neg`、`Pos`、
`Invert`，以及全部二元算术与反射算术（`Add`、`RAdd`、`Sub`、`RSub` 直到 `Eq`、`Ne`、`Lt`、
`Le`、`Gt`、`Ge`）和就地运算。

方式二，填充槽。解释器内建类型在构造函数中用 `FillSlot` 把委托挂到槽上，适合复用已有实现或
使用非虚委托：

```csharp
private PyQueueObjectType()
{
    FillSlot(PySpecialNames.Repr, ref Slots.Repr, DefaultRepr);
}
```

`FillSlots()` 是生成器在初始化时调用的虚钩子，方式一覆写的接线代码就生成在它的覆写里。

不建议在外部类型对中手动填槽或直接写 `Slots.*` 的委托字段：手动 `FillSlot` 需要自己补齐
生成器附带的接线（反射算术的 `FillReflectedSlots` 等），直接写槽字段则连类型字典的 wrapper
描述符都不会注册，`repr()`、下标这类槽分发不会走它。覆写方式一的虚方法，这些全部由生成器
代劳。

## 定义构造行为

`SomeType(args)` 的 `__new__` 通过 `[PyExport]` 部分属性与覆写 `New` 组合实现：

```csharp
[PyExport(PySpecialNames.New, nameof(NewImpl))]
private static partial PyBuiltinFunctionOrMethodObject _new { get; }

[PyFunctionParameters("maxsize=0")]
private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
{
    var result = PySpecialMethods.Index(context, arguments[0]);
    if (result.IsError)
        return result;
    return new PyQueueObject(result.Value.Int32Value);   // 返回裸对象，早于 __init__
}

protected override PyResult New(PyCallContext context, PyTypeObject cls,
    IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
{
    var obj = _new.Call(context, args, kwargs);          // 调用上面的实现
    if (obj.IsError)
        return obj;
    obj.Value._pyType = cls;                             // 支持子类化：写入实际类型
    return obj;
}
```

`[PyExport(name, methods)]` 声明一个由生成器实现的 `partial` 属性，把普通静态方法包装成
`PyBuiltinFunctionOrMethodObject`。`methods` 用 `nameof` 引用实现方法，一个名字可对应多个重载，
例如 `log` 的单参与双参版本。

`_pyType` 是 `internal` 字段，上面的写法仅库内可用。库外类型若不需要支持 Python 子类化，在
`New` 里直接返回构造好的值即可（实例的 `PyType` 回退到 `DefaultPyType`）；需要完整子类化支持
时，这是当前仅剩的库内接线点。

参数消费放在 `New` 还是 `Init`，对齐 CPython 的 `tp_new` 与 `tp_init` 分野：

- 不可变类型（如上面的 `Queue`，以及 `tuple`、`frozenset` 模式）：在 `New` 里消费参数并构造值，
  子类没有「再初始化」语义。
- 可变容器类型（dict、list、set 模式）：`New` 只分配空对象并忽略全部参数，消费可迭代参数与关键字
  参数的逻辑放在 `Init`，这样 Python 子类的自定义 `__init__` 才能取代内置初始化并接收全部构造参数。
  `__init__` 槽按 MRO 解析，见[用户类创建全流程](../internals/class-creation.md)。直接调用
  `obj.__init__(...)` 的再初始化语义由各类型自定，list 与 set 清空重填，dict 合并更新。

## 定义异常类型

```csharp
[PyException("MyPackageError", Bases = [typeof(PyValueErrorObjectType)])]
public sealed partial class MyPackageErrorObjectType : PyExceptionType;
```

基类用 `PyExceptionType`，它是 `PyTypeObject<PyExceptionObject>` 的公开派生，自带
`Shared.Create(...)` 构造异常值的助手。生成器补齐 `Bases` 覆写与 `IPyException<TSelf>` 实现。
`Bases` 指定 Python 侧基类，默认 `Exception`。

## 让类型对 Python 可见

类型对象需要挂到某个模块上才能被 `import` 到。库内的做法是在模块类上标注：

```csharp
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyQueueObjectType))]   // 注册 .Shared 单例
[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(MyModuleFunctions))]   // 扫描全部静态成员
[PyModuleInclude(PyModuleIncludeScheme.ExplicitMember, "pi", typeof(PyFloatObject), nameof(PyFloatObject.Pi))]
public partial class MyModuleObject : PyModuleObject { /* ... */ }
```

模块如何进入 `import` 体系见[用 C# 编写 Python 模块](./custom-modules.md)。类型系统的内部机制见
[对象模型](../internals/object-model.md)。
