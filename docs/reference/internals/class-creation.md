# 用户类创建全流程

源码：`PySharp/Compilation/Bytecodes/Emitter.Stmt.cs`（类语句发射）、
`PySharp/Runtime/VirtualMachine/BytecodeVirtualMachine.BytecodeImpl.cs`（`InternalBuildClass`）、
`PySharp/Runtime/PyCore.cs`（`BuildClass`）、`PySharp/Modules/Builtins/PyTypeObject.cs` 与
`PyTypeObjectOfT.cs`（`type.__new__` 与布局）、`PySharp/Modules/CSharp/UserDefinedType.cs`。

`class` 语句是语言中接线最多的语法，横跨发射器、虚拟机与类型系统三层。本篇按执行顺序走完整条链，
并指出贡献者常遇到的边界。

## 全景

```text
Emitter
    类体编译为 code object；压栈 codeObj、closure、基类与 kwargs 名
        │
        ▼
虚拟机  _BuildClass 指令 → InternalBuildClass → PyCore.BuildClass
        │
        ▼
PyCore.BuildClass
    元类决议（最派生加冲突检查）→ CreateClassBuildFrame 执行类体（locals 即命名空间）
        │
        ▼
type.__new__
    布局校验与决议 → UserDefinedType 创建
    命名空间落盘（属性、classmethod 包装、槽接线）
    __set_name__ 循环 → __init_subclass__ 链
        │
        ▼
类型对象压栈 → 装饰器逐个应用 → StoreName
```

## 1. 编译期

类语句的发射（`Emitter.Stmt.cs`）：

```text
<类体 code object>      类体编译为独立 PyCodeObject，由 ClassBuild 帧执行
<closure 占位>          非泛型为 PushNull；泛型为 type_params 元组
<基类...> <kwargs 名字元组>
_BuildClass  <bases 与 keywords 总数>
<每个装饰器>  Call 1
StoreName    <类名>
```

泛型类（PEP 695）是两层架构：内层是真正的类体 code object；外层再包一个「泛型参数 code object」，
它用 `LoadDeref` 取类型参数、`BuildTuple` 成闭包元组后执行 `_BuildClass` 并 `ReturnValue`。外层
被调用时才创建类，装饰器在外层作用域应用，对齐 CPython 的装饰器时机。

## 2. 虚拟机

弹栈顺序与压栈相反：closure（`PyTupleObject?`，非泛型为 `null`），然后类体 code object，最后
kwargs 名字元组。kwargs 值与基类经复用缓冲（`states.CacheKwargs` 与 `CacheArgs`）组装，见
[生成器与协程系统](./generator-system.md)。基类必须是 `PyTypeObject`，否则抛
`PySharpException("non-type base is not supported")`，这是当前明确的边界。随后调用
`PyCore.BuildClass`，结果类型对象压栈。

## 3. PyCore.BuildClass

```csharp
public static PyObject BuildClass(PyCallContext context, PyCodeObject codeObject,
    List<PyTypeObject> bases, OrderedDictionary<string, PyObject> kwargs, PyTupleObject? closure)
```

- 元类决议，对齐 CPython 的 `calctype`：初值取首个基类的元类；显式的 `metaclass` 关键字参数必须
  是 `PyTypeObject`（非类型元类不支持，抛 `PySharpException`）；随后遍历基类找最派生元类，
  互不为对方子类时报 `TypeError: metaclass conflict`。
- 类体执行：`CreateClassBuildFrame(codeObject, closure)` 创建 `FrameType.Class` 帧
  （`Variables.CreateForBuildingClass`，locals 是字典，闭包单元接进 free vars），`Eval` 跑完
  类体。帧的 locals 字典就是命名空间。
- 实例化元类：打包 `(name, bases, ns)` 调 `metaClass.Slots.New`；若结果确为该元类的实例且元类
  不是 `type`，再补调 `Slots.Init`，即自定义元类的 `__init__`。

## 4. type.__new__

`type(x)` 单参形式直接返回 `x.PyType`。三参形式（`_BuildClass` 的路径）依次执行：

1. 参数校验：`name` 为 `str`，`bases` 为 `tuple`，`dict` 为 `dict`，逐项错误报 `TypeError`。
2. `ValidateBasesAndResolveLayoutTypeOwner` 过三道关卡：
   - 基类的 `IsSealed` 为真时报 `TypeError: type '<name>' is not an acceptable base type`。
   - 布局兼容：`LayoutType` 是每个元类型声明的 C# 实例布局，见
     [对象模型](./object-model.md)。owner 取基类中最派生的布局，出现互不兼容的布局时报
     `TypeError: multiple bases have instance lay-out conflict`。这是 `class MyList(list)`
     的实例能复用 `PyListObject` 字段布局的机制。
   - C3 MRO 可行性：`TryCreateMROWithoutSelf` 按 merge 算法预演，失败报
     `TypeError: Cannot create a consistent method resolution order (MRO) for bases ...`。
3. 创建类型：`layoutOwner.CreateUserDefinedTypeWithSameLayout(name, qualName, bases)` 得到
   `UserDefinedType<TObject>`（internal，`DefaultModule = null`，可变类型）。`qualName` 优先取
   命名空间的 `__qualname__`，其次取 kwargs。随后 `type._pyType = cls`，类型成为元类的实例。
   模块名取当前 globals 的 `__name__`，缺省为 `"builtins"`。
4. 命名空间落盘，逐条写入 `type.PyAttributes`：
   - `__class__` 是 `PyCellObject` 时回填类型自身，即类体内 `__class__` 闭包引用的落地。
   - 名为 `__init_subclass__` 或 `__class_getitem__` 的函数自动包一层 `PyClassMethodObject`。
   - `Slots.TrySetSlot(attr, value)` 把 dunder 名直接接线到协议槽，这是用户类协议方法生效的
     地方，见[源生成器](./source-generators.md)。
   - 类体隐式键与 `__qualname__` 的去向，对齐 CPython 的 `codegen_class_body` 与
     `type_new_set_ht_name`：类体前导注入三键 `__module__`、`__qualname__`、
     `__firstlineno__`（类体 `locals()` 可见，`__firstlineno__` 取类语句首行，带装饰器时取首个
     装饰器行；`__qualname__` 由语义模型按词法栈组装）。type 创建时 `__qualname__` 移入类型
     访问器，不保留为类字典条目，非 `str` 值报 `TypeError`；命名空间保留 `__module__` 与
     `__firstlineno__`，前者优先于全局 `__name__`，后者是普通类属性。显式 `__qualname__` 覆盖
     时，类 `__name__` 以构造入参重设。CPython 3.13 同族的 `__static_attributes__` 尚未注入，
     见[路线图](../contributing/roadmap.md)。
5. `__class_getitem__` 自动注入：命名空间含 `__type_params__` 且 MRO 中无 `__class_getitem__`
   时，注入默认实现（返回 `GenericAlias`）。
6. `__set_name__`：遍历新类型的全部属性，对有 `__set_name__` 槽的逐个调用 `(owner, name)`。
7. `__init_subclass__` 协作链：按 CPython 的 `type_new_init_subclass` 模式调用
   `super(type, type).__init_subclass__(**kwargs)`，沿 MRO 触发各基类实现。

实例化路径（`type.__call__`，`PyTypeObjectType.Call` 覆写）为标准序：先 `Slots.New`，结果是
`cls` 实例且有 `Slots.Init` 时补 `__init__`。槽位按 MRO 重新解析：用户类创建完成后按 MRO 重查
`__new__` 与 `__init__` 槽，因此多继承下先创建基类的内置实现不再冻结槽位、屏蔽后定义基类的
`__init__`，对齐 CPython 的 fixup slot dispatch。容器子类（dict、list、set）据此拆分 `__new__`
（仅分配）与 `__init__`（消费参数），子类自定义 `__init__` 取代内置初始化并接收全部构造参数。

异常子类的 `args` 回绑是特例。`BaseException` 自有 `Init` 槽，对齐 CPython 的
`BaseException_init`：子类覆盖 `__new__` 而不覆盖 `__init__` 时，`type.__call__` 仍会以原始
实例化参数调用继承的 `BaseException.__init__`，后者回绑 `e.args` 元组。该槽同时拒绝关键字参数，
报 `TypeError: <Cls>() takes no keyword arguments`，消息带类名，并让异常实例不落入
`object.__init__` 的多余参数检查。

## 贡献者提示

- 三个用户可见的 `TypeError` 入口都在 `ValidateBasesAndResolveLayoutTypeOwner` 与元类决议里，
  改继承语义时先过这里的既有分支。
- 明确的不支持边界（抛 `PySharpException`，非用户错误）：非类型元类、非类型基类、bases 元素
  非类型。扩展前先决定它们该变成什么 Python 错误。
- 给内建类型补 `IsSealed` 时注意它同时关闭了用户继承路径。
- 回归族包括 `test_class_*`（simple、inherit、nested、closure、special_methods）、
  `test_metaclass.py` 与 `test_metaclass_kwargs`、`test_name_mangling.py`、`test_generic_*`、
  `test_user_defined_descriptor.py`、`test_property.py`，
  以及 `test_exception_custom_new_args_regression.py`（异常子类 args 回绑）、
  `test_qualname_metadata_regression.py`（qualname、`__firstlineno__` 与函数及生成器元数据）、
  `test_container_init_regression.py`（容器子类构造拆分），见[测试体系](./testing.md)。

## 相关阅读

[对象模型](./object-model.md)（类型对与布局）· [虚拟机](./virtual-machine.md)（帧执行）·
[字节码](./bytecode/README.md)（`_BuildClass` 指令）· [语义分析](./semantic-analysis.md)
（类作用域规则）
