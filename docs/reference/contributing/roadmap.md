# 路线图与可认领缺口

本文是非承诺性的贡献地图：0.x 阶段优先级随演进调整，认领前先在 issue 或讨论中对齐方向。

缺口来源包括[与 CPython 的差异](../python-compat/cpython-differences.md)（用户视角限制）、语言与标准库支持面（[语言特性](../python-compat/language-features.md)、[标准库覆盖](../python-compat/stdlib-modules.md)、[内建类型方法面](../python-compat/builtin-type-methods.md)），以及源码内的 `TODO` 标记（当前 25 处）。按性质分档如下。

## 一、兼容性与标准库

对脚本作者价值最高的一档。

| 缺口 | 现状与入手点 | 难度 |
| --- | --- | --- |
| 标准库扩展 | `os`、`re`、`json`、`collections`、`itertools`、`functools`、`pathlib`、`datetime` 等不可用。新模块的完整流程见[新增类型与模块](./adding-types-and-modules.md)，纯 Python 实现可走[冻结模块](../user-guide/custom-modules.md)路线 | 每模块独立，可拆分认领 |
| 内建类型方法面补全 | 覆盖清单见[内建类型方法面覆盖](../python-compat/builtin-type-methods.md)。`bytes` 仅有 `decode`，`bytearray` 仅有 `append` 与 `extend`；`int` 没有任何方法（缺 `bit_length`、`to_bytes`、`from_bytes` 等）；`range` 缺 `index`、`count` 与 `.start`、`.stop`、`.step` 属性；`str` 缺 `maketrans`、`translate`、`format_map`。`list`、`tuple`、`dict`、`set`、`frozenset`、`float` 已与 CPython 方法面一致。建议的认领顺序按价值与成本比降序：`range` 的三个只读属性 `.start` / `.stop` / `.step`（成本最低、最常用），`int` 方法面（纯 C# 侧转换），`bytes` 高频方法（`startswith`、`endswith`、`strip` 族、`find`、`count`，语义可参照 str 的同名实现），`bytearray` 补齐，最后是 `str.translate` 族。新增方法遵循 `[PyMethod]` 声明加回归（参照[新增类型与模块](./adding-types-and-modules.md)） | 每类型独立，小步认领 |
| `sys` 成员补全 | 目前提供 `sys.argv`、`stdin` / `stdout` / `stderr` 流包装与 `get_int_max_str_digits` / `set_int_max_str_digits`；尚缺 `sys.modules`、`sys.path` 的 Python 侧可见面，以及 `sys.flags`、`sys.version` 等信息量成员 | 小 |
| `__static_attributes__` 类体隐式键 | CPython 3.13 起在类体编译期收集方法体内对首参（`self` / `cls`）的属性名元组，供 introspection 与内联优化使用。PySharp 尚未注入，`cls.__static_attributes__` 报 `AttributeError`；入手点是类语句发射处（紧邻已有的 `__firstlineno__` 注入点）与语义分析器的首参属性访问收集 | 中 |
| `.pyc` 字节码缓存 | 每次 import 重新编译；需要缓存键设计（源文件 mtime 或哈希）与 `Bytecode` 的序列化 | 大 |
| 错误消息对齐 | 措辞与 CPython 仍有出入，判别尺度见[错误消息规范](./error-messages.md)；可逐条对齐并补回归。运算符报文三模板、str 方法族 TypeError、容器构造参数校验、异常链属性删除报文、len 上界、int 转换位数限制等已完成对齐 | 小 |
| `__del__` 与终结器语义 | 依赖 .NET GC，时机与 CPython 的引用计数不同；需要明确文档化，或补充显式终结协议 | 中（先设计后动手） |

## 二、嵌入体验

对 C# 使用者价值最高的一档。这一档多为政策变更而非纯实现，动工前先读[公共 API 稳定性政策](./api-stability.md)。

| 缺口 | 现状与入手点 | 难度 |
| --- | --- | --- |
| 读取模块全局变量的公共 API | `PyModuleObject.PyAttributes` 为 internal，`RunCode` / `RunFile` 的结果取值靠 stdout 重定向（见[执行文档的限制说明](../user-guide/executing-python.md)）。开放它需要决定 API 形态（索引器或 `GetAttr` 快捷方法）与安全边界 | 中 |
| `PyCallContext` 的受控公开 | 这是协议操作 API（`Call`、`GetAttr`、`PyOperators`）对宿主代码不可用的根源；需要设计无帧上下文的语义边界 | 大（政策级） |
| 自定义模块提供器接线 | `PyModuleProvider.Create` 已是公共 API，但挂载点 `ModuleProviders` 为 internal；补一个环境构建器入口即可闭环 | 小 |
| C# 与 Python 值的通用封送 | 目前手工走 `Py*Object` 工厂与访问器；一个约定式的 `ToPython()` / `As<T>()` 层可以大幅降低嵌入成本，前提是保持零反射 | 中 |
| 组异常的公共遍历 API | 组结构访问器（`IsGroup`、`AsGroup`、`ExceptionGroupInfo`）当前全为 internal，C# 侧只能靠 `Args` 探测，形态随构造路径而异（见[异常组内幕](../internals/exception-groups.md)）。开放需要决定公共面形态（子异常枚举器、`Message`、嵌套组递归） | 中（政策级） |
| CLI 选项 | `-m` 与 `-i` 未实现；`-O`、`-OO`、`-S`、`-`（stdin）与 `--` 终止符已落地，见[命令行与交互模式](../user-guide/cli-and-repl.md) | 小 |

## 三、运行时与对象系统

以下是源码内 `TODO` 标记对应的改进点（`grep -rn "TODO"` 可复现，排除 `obj/`），四档合计 25 处。

| 线索 | 位置（代表性） |
| --- | --- |
| 异常处理器的回滚粒度 | `BytecodeVirtualMachine.cs` |
| f-string 无效转义的告警 | `Lexer` 的 `FStringMiddle` 扫描处 |
| 不可变类型的定义方式 | `PyTypeObjectOfT.Virtual.cs` |
| 类 `__dict__` 非字符串键的 `RuntimeWarning` | `PyTypeObjectOfT.cs`，见[类创建](../internals/class-creation.md) |
| `PyTypeSlots` 的协议分组泛化 | `PyTypeObject.Slots.cs` |
| `CallMethod` 的 `GetAttrOrMethod` 优化路径 | `PyObjectCallExtensions.cs` |
| 线程中断后的帧状态恢复 | `PyThreadObject.Py.cs`，见 [threading 与 queue](../internals/threading-and-queue.md) |
| `exec` / `eval` 的 globals 映射与序列泛化 | `PyCore.cs` 两处 |
| `import *` 的 `__all__` 异常路径错误化 | `PyEnvironment.Import.cs` |
| dict 删除与重建的性能（`perf` 标记） | `PyDictObject.cs` 多处，见 [dict 系统](../internals/dict-system.md) |

## 四、工程与治理

- 发布元数据补齐：csproj 尚缺 `RepositoryUrl`、`License`、SourceLink 与符号包，见[发布流程](./release-process.md)；
- `PySharp.Analyzer` 的版本号与独立发版节奏，当前未显式设置 `<Version>`；
- 生成器与分析器没有单元测试，依赖自举验证，见[源生成器](../internals/source-generators.md)；`Microsoft.CodeAnalysis` 的 generator snapshot 测试是可选补强方向；
- 文档同步义务：改公共行为时同步 [API 参考](../api/)与兼容性文档。

## 认领指南

1. 先确认缺口是否仍存在，不要依赖本文的版本锚点；大项先开讨论对齐设计，尤其第二档的政策级条目；
2. 实现遵循对应操作指南与[编码规范](./coding-standards.md)；语义行为必须附带 `test_pyfiles` 回归，见[测试体系](../internals/testing.md)；
3. 与 CPython 行为对齐的条目以 CPython 为准绳落回归脚本，参照既有 `test_*_regression.py` 的最小复现风格；
4. 小步提交，一个缺口一笔 `feat:` 或 `fix:`，性能类提交附前后对比数据。
