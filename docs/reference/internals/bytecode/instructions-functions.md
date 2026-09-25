# 指令参考：函数、类与模块（Functions / Classes / Modules）

行为依据：`Runtime/VirtualMachine/BytecodeVirtualMachine.cs` 与 `.BytecodeImpl.cs`
（`InternalMakeFunctionWithPyArgsDef`、`InternalBuildClass`、`InternalSetupAnnotations`、
`InternalImportName`）。类创建全流程见[用户类创建全流程](../class-creation.md)。

通用约定见[常量与名字篇](./instructions-loads.md#通用约定全部指令参考篇适用)。

## 函数定义

### `_MakeFunctionWithPyArgsDef` — 创建函数（带签名）

- **Arg**：无；**栈效应**`(defaults_tuple, kwdefaults_tuple, code → func)`。
- `def` 语句的落点。栈布局自底向上：**位置缺省值元组 → 关键字缺省值元组 → 代码对象**（探针实例：`LoadConst 10; BuildTuple 1; LoadConst EmptyTuple; LoadConst <code 'add'>; _MakeFunctionWithPyArgsDef`）。无缺省的槽位以 `null` 占位（`PushNull` 产物，见[常量与名字](./instructions-loads.md#pushnull--压入-null-占位)），组装时解释为"该参数无缺省"。`PyArgsDef.FromCodeObjectAndDefaults` 把代码对象签名与两组缺省合成运行期签名表，`PyCore.MakeFunction` 产出 `PyFunctionObject`。
- **错误**：无（缺省值合法性在编译期保证）。

## 闭包单元

### `MakeCell` — 建空 cell（按名）

- **Arg**：`name[i]`；**栈效应**：无。
- 以空 `PyCellObject` 写入该名字的存储槽，即**声明** cell 变量（闭包捕获变量的存储初始化），此后 `_StoreDerefFast` 等指令操作它。

### `_MakeCellFast` — 值包 cell（按槽位，内部）

- **Arg**：localsPlus 槽位下标；**栈效应**：无（栈外原地操作）。
- 读取槽位当前**值**、包装为 cell 后写回同槽，是"先有值后升级为 cell"的快速路径（避免按名查表）。与 `MakeCell` 的分工：前者建空壳，后者原地升级。

## 类定义

### `_BuildClass` — 创建类

- **Arg**：基类数 + 关键字实参**值**个数；**栈效应**`(bases…, kw_values…, kw_names_tuple, code, closure → type)`。
- `class` 语句的落点，栈布局自底向上：**基类们 → 元类等关键字实参值 → 关键字名字元组 → 类体代码对象 → closure**。closure 在顶：非泛型类是 `None`，泛型类（PEP 695）是类型参数 cell 元组（作为类体帧的自由变量）。关键字实参按 `CallKw` 同款方式收集进 `CacheKwargs`；基类**必须全部是类型对象**。真正的创建流程（元类决议、布局、`type.__new__` 七步、`__init_subclass__` 链）在 `PyCore.BuildClass`，见[用户类创建全流程](../class-creation.md)。
- **错误**：非类型基类 → 异常（"non-type base is not supported"）；其余见类创建流程。

## 类型别名与类型参数（PEP 695）

### `_MakeTypeAlias` — 创建类型别名

- **Arg**：`name[i]`；**栈效应**`(func → typealias)`（原地替换）。
- `type X = …` 语句：把栈顶的别名主体函数包装为 `PyTypeAliasTypeObject(名字, func)`。

### `_SetFunctionTypeParams` — 记录函数类型参数

- **Arg**：无；**栈效应**`(type_params →)`（弹出并挂到栈顶函数上）。
- 弹出类型参数元组，写入其下函数对象的 `__type_params__` 属性，即 `def f[T]()` 的 T 记录步骤（泛型函数三步发射的收尾）。

## 模块导入

### `ImportName` — 导入模块

- **Arg**：`name[i]`（模块名）；**栈效应**`(level, fromlist → module)`。
- 栈布局 `[level, fromlist]`（fromlist 在顶）。`level > 0` 时按调用者的 `__package__` / `__name__` / `__path__` 解析相对导入为绝对名；随后 `TryLoadModule` 加载。**压栈对象按 fromlist 分流**（对齐 CPython `_handle_fromlist`）：`from a import x` 的非空 fromlist → 逐项尝试把 x 作为子模块导入（存在则挂到包属性上）并压**子模块所在模块**；`import a.b.c` 的空 / None fromlist → 压**根模块** a。import 解析顺序（sys.path 逐目录 / 内嵌模块表）见[Environments 与模块解析](../environments-and-modules.md)。
- **错误**：模块找不到 → `ModuleNotFoundError`。

### `ImportFrom` — 从模块取名字

- **Arg**：`name[i]`（待取名字）；**栈效应**`(module → module, attr)`（保留模块、压入属性）。
- `from m import x` 的提取步骤：对栈顶模块 `GetAttr` 取名（经 PEP 562 模块 `__getattr__` 钩子）。后续 `Store*` 绑定到当前作用域。
- **错误**：名字不存在 → `ImportError: cannot import name '<name>' from '<module>'`，对齐 CPython；早期实现误报 `AttributeError`。

## 注解

### `SetupAnnotations` — 初始化 `__annotations__`

- **Arg**：无；**栈效应**：无。
- 按帧形态在**类体帧的 locals** 或**模块帧的 globals** 中创建空的 `__annotations__` 字典（已存在则不动），这是变量注解（`x: int`）写入前的准备步骤。
- **错误**：无。

## 相关阅读

[用户类创建全流程](../class-creation.md)（`_BuildClass` 之后的完整链路）· [语义分析](../semantic-analysis.md)（cell / free 分类）· [Environments 与模块解析](../environments-and-modules.md)（import 的解析顺序）· [调用与属性下标](./instructions-calls.md)（`CallKw` 同款关键字收集）
