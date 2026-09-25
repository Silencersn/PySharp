# 扩展 PySharp

本篇是扩展开发的入口。自定义类型与模块的细节见[用 C# 定义 Python 类型](./custom-types.md)与
[用 C# 编写 Python 模块](./custom-modules.md)，生成器内幕见
[源生成器与代码分析](../internals/source-generators.md)。

PySharp 把「C# 能力暴露到 Python 层」做成一条声明式流水线：在 C# 代码上标注特性（attribute），
Roslyn 增量源生成器在编译期读取这些特性并产出注册代码（方法描述符、协议槽、模块成员装配），
运行时不需要反射。库自身的全部内建类型与标准库模块都以此方式组织，因此对 AOT 与 trimming 友好。

## 工作模型

```text
C# 声明（[PyType] [PyMethod] [PyExport] ...）
    ↓  增量源生成器（编译期）
.g.cs 注册代码（Shared / FillSlots / RegisterMethods / ApplyIncludes）
    ↓
运行时类型机器（slots + MRO + 描述符）
    ↓
Python 层（geom.Vector、geom.area_of_circle(...)）
```

在 C# 里只写行为本身，即一个 `static PyResult` 方法或一个协议覆写；「叫什么名字、挂到哪里、
参数怎么校验」由特性声明，样板由生成器补齐。

## 前置条件

项目引用 `PySharp.csproj`。源码引用时公开生成器已随 `OutputItemType="Analyzer"` 接入。
扩展代码常用的命名空间：

```csharp
using PySharp.Modules.Builtins;     // PyObject、Py*Object、PySpecialNames、异常类型
using PySharp.Runtime;              // PyResult、PySpecialMethods、PyOperators
using PySharp.Runtime.Calls;        // PyCallContext、PyArguments
using PySharp.Runtime.PyAttributes; // 扩展特性
```

## 特性速查

类型层：

| 特性 | 标注目标 | 作用 |
| --- | --- | --- |
| `[PyType("qual.name")]` | 元类型类 | 声明 Python 类型；`Module` 默认 `"builtins"`，`IsSealed` 默认 `false` |
| `[PyTypeConstructor]` | 元类型类 | 控制生成器是否代写构造函数与 `Shared` 单例（`DoNotGenerateConstructor`、`AccessModifier`） |
| `[PyException("Name")]` | 异常元类型类 | 生成 `PyExceptionType` 派生的异常类型；`Bases` 指定 Python 基类，默认 `Exception` |

成员层：

| 特性 | 标注目标 | 作用 |
| --- | --- | --- |
| `[PyMethod("name")]` | 静态方法 | 实例方法，`self` 为强类型；`Order` 控制注册顺序 |
| `[PyStaticMethod("name")]` | 静态方法 | 静态方法 |
| `[PyClassMethod("name")]` | 静态方法 | 类方法，首参为类型对象 |
| `[PyProperty("name")]` | 静态方法 | 属性访问器，`Type` 取 `Getter`、`Setter`、`Deleter` 三件套并按名合并 |
| `[PyExport("name", nameof(Impl), ...)]` | `partial` 静态属性 | 包装为 `PyBuiltinFunctionOrMethodObject`；`nameof` 引用实现方法，一个名字可合并多重载 |
| `[PyFunctionParameters("a", "b=1", ...)]` | 实现方法 | 声明 Python 形参签名，支持默认值、`/`、`*`、`**kwargs` 记法，生成器据此生成免反射的参数绑定 |

模块层：

| 特性 | 标注目标 | 作用 |
| --- | --- | --- |
| `[PyModuleInclude(Scheme, typeof(T))]` | `PyModuleObject` 子类 | 组装模块成员。`StaticMembers` 扫描全部公共静态 `PyObject` 成员，`TypeSingleton` 挂载 `T.Shared` 类型，`ExplicitMember` 按显式名挂载指定成员 |
| `[PyFrozenModule("name", "path.py")]` | `PyFrozenModuleObject` 子类 | 把 `.py` 源码（位于 csproj 的 `AdditionalFiles`）在编译期嵌入，import 时编译执行 |

## 方法签名约定

实现方法的 C# 签名由目标委托类型决定，定义在 `PySharp.Runtime.Calls.PyDelegates`：

| 声明形式 | 委托 | 适用 |
| --- | --- | --- |
| `PyResult M(PyCallContext, PyArguments)` | `PyFunction` | 模块级函数、`[PyExport]` 实现、`staticmethod` |
| `PyResult M(PyCallContext, T self, PyArguments)` | `PyMethod<T>` | `[PyMethod]` 实例方法，`T` 为值类型 |
| `PyResult M(PyCallContext, PyTypeObject cls, PyArguments)` | 无 | `[PyClassMethod]` |
| `PyResult M(PyCallContext, T self)` | `PyMemberGetter<T>` | `[PyProperty]` getter |
| `PyResult M(PyCallContext, T self, PyObject value)` | `PyMemberSetter<T>` | `[PyProperty]` setter |

`PyArguments` 按形参序号取绑定值，即 `arguments[0]`。缺参、多参与未知关键字已按
`[PyFunctionParameters]` 的声明校验，并给出 Python 风格的 `TypeError`。返回值成功时直接
`return obj;`（隐式转换），失败时用 `PyResult.TypeError("...")` 等异常工厂，见
[PyResult 参考](../api/PyResult.md)。

抛出自定义异常类型时要注意：`PyResult.TypeError(...)` 等工厂只覆盖内建异常。抛出用 `[PyException]`
定义的类型时，用 `PyExceptionResult` 的公共构造（可隐式转 `PyResult`）或 `PyResult.FromException`：

```csharp
return new PyExceptionResult(MyErrorObjectType.Shared.Create(PyStrObject.FromString("...")));
// 等价写法：return PyResult.FromException(MyErrorObjectType.Shared.Create(...));
```

这里有一个语义陷阱：`return exc;` 直接返回 `PyExceptionObject` 会走 `PyObject` 到 `PyResult`
的成功值转换，异常会被当成普通返回值。必须包一层 `PyExceptionResult` 或 `FromException`。

## 端到端示例

以下示例完整走一遍异常、类型（构造、方法、属性、协议）、模块函数与模块组装。写法取自库内
`queue` 与 `math` 模块：

```csharp
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace MyExt.Geometry;

// 1. 异常类型：一行声明
[PyException("GeomError", Bases = [typeof(PyValueErrorObjectType)])]
public sealed partial class GeomErrorObjectType : PyExceptionType;

// 2. 值类型：只承载数据
public sealed partial class PyVectorObject : PyObject
{
    public double X { get; }
    public double Y { get; }

    public override PyTypeObject DefaultPyType => PyVectorObjectType.Shared;

    internal PyVectorObject(double x, double y) => (X, Y) = (x, y);
}

// 3. 元类型：构造、方法、属性、协议
[PyType("Vector", Module = "geom")]
public sealed partial class PyVectorObjectType : PyTypeObject<PyVectorObject>
{
    // 构造：__new__ 经 [PyExport] 实现，生成器补 _new 属性实现
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("x", "y")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var x = PySpecialMethods.Float(context, arguments[0]);
        if (x.IsError) return x;
        var y = PySpecialMethods.Float(context, arguments[1]);
        if (y.IsError) return y;
        return new PyVectorObject(x.Value.Value, y.Value.Value);
    }

    // 实例方法
    [PyMethod("length")]
    [PyFunctionParameters()]
    private static PyResult Length(PyCallContext context, PyVectorObject self, PyArguments arguments)
        => PyFloatObject.FromDouble(Math.Sqrt(self.X * self.X + self.Y * self.Y));

    [PyMethod("scale")]
    [PyFunctionParameters("factor")]
    private static PyResult Scale(PyCallContext context, PyVectorObject self, PyArguments arguments)
    {
        var f = PySpecialMethods.Float(context, arguments[0]);
        if (f.IsError) return f;
        return new PyVectorObject(self.X * f.Value.Value, self.Y * f.Value.Value);
    }

    // 属性 getter
    [PyProperty("x")]
    private static PyResult Get_X(PyCallContext context, PyVectorObject self)
        => PyFloatObject.FromDouble(self.X);

    // 协议：覆写虚方法，可覆写项有 90 余个，见 custom-types.md
    protected override PyResult Repr(PyCallContext context, PyVectorObject self)
        => PyStrObject.FromString($"Vector({self.X}, {self.Y})");

    protected override PyResult Add(PyCallContext context, PyVectorObject self, PyObject other)
        => other is PyVectorObject v
            ? new PyVectorObject(self.X + v.X, self.Y + v.Y)
            : PyNotImplementedObject.NotImplemented;   // 交给反射协议，或最终报 TypeError
}

// 4. 模块函数：[PyExport] partial 属性
internal static partial class GeomFunctions
{
    [PyExport("area_of_circle", nameof(AreaOfCircleImpl))]
    public static partial PyBuiltinFunctionOrMethodObject AreaOfCircle { get; }

    [PyFunctionParameters("r")]
    private static PyResult AreaOfCircleImpl(PyCallContext context, PyArguments arguments)
    {
        var r = PySpecialMethods.Float(context, arguments[0]);
        if (r.IsError) return r;
        if (r.Value.Value < 0)
            return new PyExceptionResult(GeomErrorObjectType.Shared.Create(
                PyStrObject.FromString("radius must be non-negative")));
        return PyFloatObject.FromDouble(Math.PI * r.Value.Value * r.Value.Value);
    }
}

// 5. 模块：组装类型与函数，暴露给 Python
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyVectorObjectType))]   // geom.Vector
[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(GeomFunctions))]        // geom.area_of_circle
[PyModuleInclude(PyModuleIncludeScheme.ExplicitMember, "GeomError", typeof(GeomErrorObjectType), nameof(GeomErrorObjectType.Shared))]
public partial class GeomModuleObject : PyModuleObject
{
    public GeomModuleObject() : base("geom") { }
}
```

生成器为以上声明产出的内容：`PyVectorObjectType.g.cs` 包含 `Shared` 单例、`FillNewSlot`、
`RegisterMethods` 的两个 `AppendMethodDescriptor` 与 `RegisterProperties` 的
`AppendMemberDescriptor`；此外还有 `GeomErrorObjectType.Exception.g.cs`、
`GeomFunctions.PyExport.g.cs`，以及 `GeomModuleObject.PyModuleInclude.g.cs`（覆写
`ApplyIncludes` 生成 `AppendAttribute` 调用）。

Python 侧的使用形态：

```python
import geom

v = geom.Vector(3, 4)
print(v)             # Vector(3, 4)          Repr 覆写
print(v.length())    # 5.0                  [PyMethod]
print(v.x)           # 3.0                  [PyProperty]
w = v + v            # Vector(6, 8)         Add 覆写
print(geom.area_of_circle(1))   # 3.14159...
```

## 注册与可见性

`import` 只认环境的模块解析链，即内建注册表与 `sys.path` 文件系统。因此组装好的模块对象还需要一个
进入解析链的入口，当前有两条路：

1. 库内扩展（给标准库添加模块，或参与库开发）：在 `PyStandardLibrary.TryCreateModule` 注册
   `"geom" => new GeomModuleObject()`。这是库自身 `math`、`queue` 等模块的方式。
2. 嵌入场景：把模块以纯 Python 源码放进 `MemoryFileSystem` 并让 `sys.path` 指过去，见
   [虚拟文件系统](./virtual-file-system.md)。若其中的逻辑必须用 C# 实现，当前版本的公开接线尚在
   演进，见[与 CPython 的差异](../python-compat/cpython-differences.md)。

另有两个 `internal` 边界，目前完整的类型对体验以库内为主：

- 支持子类化的 `obj.Value._pyType = cls` 写法使用了 internal 字段。库内完整模式见
  `PyQueueObjectType`。
- `PyCallContext` 仅由运行时在扩展点内提供。实现方法签名中的 `context` 参数就是入口，可在其中
  使用 `PyOperators` 与 `PySpecialMethods` 全套协议 API。

## 编译期反馈

- 生成器诊断（`PYARG`，以错误为主）：`PYARG010` 表示 `[PyMethod]` 方法缺少
  `[PyFunctionParameters]`，无法生成免反射绑定；`PYARG006` 到 `PYARG009` 对应 `[PyExport]`
  的实现方法找不到、缺少签名标注、签名不兼容或名字为空；`PYARG011` 表示 `PyTypeObject<T>`
  的类型参数解析失败；`PYARG005` 表示 `[PyFrozenModule]` 引用的 `.py` 不在 `AdditionalFiles`。
- `PYSP` 分析器同样约束扩展代码：用 `return obj;` 而非 `FromValue(obj)`（PYSP001）、用
  `PySpecialNames` 常量而非池查找（PYSP003）、用常量代替工厂（PYSP004）等，见
  [编码规范](../contributing/coding-standards.md)。

## 深入阅读

- [用 C# 定义 Python 类型](./custom-types.md)：类型对全集、协议覆写清单、`FillSlot`
- [用 C# 编写 Python 模块](./custom-modules.md)：`[PyModuleInclude]` 细则与冻结模块
- [源生成器与代码分析](../internals/source-generators.md)：每个生成器产出的内容
- [在 C# 中操作 Python 对象](./python-objects-from-csharp.md)：实现内部可用的值构造与读取 API
