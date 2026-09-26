# 用 C# 编写 Python 模块

范例源码：`PySharp/Modules/Mathematics/`、`PySharp/Modules/This/PyThisModuleObject.cs`。
本篇是[扩展 PySharp](./extending-pysharp.md)的模块子主题。

给嵌入的 Python 代码添加模块有两条路，按成本从低到高：

1. 纯 Python 模块加虚拟文件系统（推荐，无需 C# 代码）：把 `.py` 源码放进 `MemoryFileSystem`，
   让 `sys.path` 指过去即可 `import`。
2. C# 实现的模块对象：继承 `PyModuleObject`，用源生成器特性组装成员。这是解释器标准库自身的组织
   方式，适合参与库开发的场景。

## 方式一：内存文件系统中的 Python 模块

```csharp
using PySharp.Runtime;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO.Memory;

var fs = MemoryFileSystem.CreateBuilder()
    .WithFile("/lib/calc.py", """
        def add(a, b):
            return a + b
        """)
    .Build();

var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(Console.OpenStandardOutput())
    .UseFileSystem(fs)
    .Build();

using var environment = host.CreateEnvironmentBuilder()
    .AddArg("-c")
    .AddPath("/lib")
    .Build();

using var interpreter = PyInterpreter.Create(environment);
interpreter.Execute("from calc import add\nprint(add(20, 22))", "<main>");   // 42
```

包（`__init__.py`）、子模块与相对导入都按目录结构正常工作。详见
[虚拟文件系统](./virtual-file-system.md)。

## 方式二：C# 模块对象

### 模块骨架与成员组装

以 `math` 模块为例：

```csharp
[PyModuleInclude(PyModuleIncludeScheme.ExplicitMember, "pi", typeof(PyFloatObject), nameof(PyFloatObject.Pi))]
[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PyMathFunctions))]
public partial class PyMathModuleObject : PyModuleObject
{
    public PyMathModuleObject() : base("math") { }
}
```

`PyModuleObject` 的公共面：

| 成员 | 说明 |
| --- | --- |
| `PyModuleObject(string name)` | 构造时写入 `__name__` 与 `__package__`，并触发 `ApplyIncludes`（由生成器覆写） |
| `string Name { get; }` | 模块名 |
| `string? Origin { get; }` | 来源标注；路径模块为文件路径，冻结模块为 `"frozen"`，内建模块为 `null` |
| `virtual void OnImport(PyCallContext context, PyEnvironment environment)` | 每次 import 时的初始化钩子 |
| `protected virtual void ApplyIncludes()` | 构造期注册成员；存在 `[PyModuleInclude]` 时由源生成器覆写 |
| `protected internal void AppendAttribute(string name, PyObject pyObject)` | 手动向模块追加成员 |

`PyModuleIncludeScheme` 有三种方案：

- `StaticMembers`：扫描指定类型的全部静态 `PyObject` 成员，配合 `[PyExport]` 属性使用。
- `TypeSingleton`：注册指定元类型的 `.Shared` 单例，即把一个 Python 类型挂进模块。
- `ExplicitMember`：以显式名字注册指定类型的某个静态成员，如上面的 `pi`。

### 用 PyExport 定义函数

```csharp
internal static partial class PyMathFunctions
{
    [PyExport("sqrt", nameof(SqrtImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Sqrt { get; }

    [PyExport("log", nameof(LogImpl_1), nameof(LogImpl_2))]     // 多重载合并为一个 Python 函数
    public static partial PyBuiltinFunctionOrMethodObject Log { get; }

    [PyFunctionParameters("x", "/")]
    private static PyResult SqrtImpl(PyCallContext context, PyArguments arguments)
    {
        // 参数校验由 [PyFunctionParameters] 声明驱动
    }
}
```

`[PyExport(name, methods)]` 作用于 `partial` 属性。生成器读取被引用方法上的
`[PyFunctionParameters]`，在编译期确定形参签名并生成属性实现，运行时不需要反射。

### 冻结模块

既想用 Python 写模块、又想嵌进程序集，可以用 `PyFrozenModuleObject` 加 `[PyFrozenModule]`，`this`
模块就是这么做的：

```csharp
[PyFrozenModule("this", @"Lib\this.py")]    // .py 需在 csproj 中声明为 AdditionalFiles
public partial class PyThisModuleObject : PyFrozenModuleObject;
```

生成器在编译期读取该文件填充 `Code` 属性，首次 import 时编译并执行，`Origin` 为 `"frozen"`。

## 模块解析与 PyModuleProvider

`import` 按环境的模块提供器链解析，命中第一个即止，默认链为：

1. `PyModuleProvider.Builtin`：查询 `PyStandardLibrary` 注册表，即 `builtins`、`sys`、`math`
   等内建模块。
2. `PyModuleProvider.Path`：扫描 `sys.path` 与环境文件系统，加载 `.py` 文件或包目录。

`PyModuleProvider` 是公共类型，采用与 CPython PEP 451 对应的两阶段协议：

```csharp
public abstract class PyModuleProvider
{
    public static PyModuleProvider Builtin { get; }
    public static PyModuleProvider Path { get; }

    // 阶段一：定位并构造模块（可以"造即完整"，如内建注册表）
    public abstract bool TryCreateModule(PyCallContext context, string fullName,
        IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module);

    // 阶段二：执行模块体。默认空实现，适用于 create 阶段已产出完整模块的提供器
    protected internal virtual void ExecModule(PyCallContext context, PyModuleObject module);

    public static PyModuleProvider Create(IDictionary<string, Func<PyModuleObject>> mapping);
}
```

`Create(mapping)` 用「名字到工厂委托」的字典构造自定义提供器。两阶段的编排由导入机制层负责：
`TryCreateModule` 命中后，机制层先把模块登记进模块缓存，再调 `ExecModule` 执行模块体，最后调
模块的 `OnImport` 钩子——与 CPython 先往 `sys.modules` 放模块再 `exec_module` 一致，模块体或
`OnImport` 里的循环导入与自引用 import 看到的是缓存中的半初始化模块，不会重入提供器；初始化抛
异常时登记被回滚，下一次 import 从头重试。

挂载入口是环境构建器（顺序即解析优先级，`import` 沿链命中第一个即止）：

```csharp
using var environment = host.CreateEnvironmentBuilder()
    .AddModuleProvider(provider)        // 追加链尾：仅兜底，Path 命中的名字不受影响
    .InsertModuleProvider(provider)     // 插到链头：遮蔽内建注册表与磁盘文件（同 sys.meta_path.insert(0, …)）
    .ClearModuleProviders()             // 清空默认链后自建（后续 Add 逐一挂回）
    .Build();
```

标准库注册表（`PyStandardLibrary`）仍只在库内扩展；要在嵌入侧补模块，用自定义提供器或
[虚拟文件系统](./virtual-file-system.md)承载。模块解析机制的内部细节见
[Environments 与模块解析](../internals/environments-and-modules.md)。
