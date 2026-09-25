# 警告与数据类

源码：`PySharp/Modules/Warnings/`、`PySharp/Lib/dataclasses.py`。

本篇说明 `warnings` 与 `dataclasses` 两个标准库模块的用法。逐函数覆盖清单见
[标准库模块覆盖](../python-compat/stdlib-modules.md)。

## warnings

警告沿环境的错误流输出，格式为 `文件名:行号: 警告类别: 消息`。过滤器与去重状态是每解释器的，
绑定在 `PyEnvironment` 上，跨环境互不影响：

```csharp
var envA = MakeEnvironment();   // 脚本里 simplefilter("error")  → warn 抛异常
var envB = MakeEnvironment();   // 脚本里 simplefilter("ignore") → warn 静默
```

需要全局一致的警告策略时，在各环境的初始化脚本里执行同样的 `simplefilter` 或 `filterwarnings`
调用即可。

### 发出警告

```python
import warnings

warnings.warn("deprecated soon")                    # 类别默认 UserWarning
warnings.warn("cache miss", RuntimeWarning)         # 指定类别
warnings.warn("caller view", stacklevel=2)          # 归因到调用方

class MyWarning(Warning):                           # 自定义类别：继承 Warning 即可
    pass

warnings.warn("custom", MyWarning)
```

`warn_explicit(...)` 是显式定位版，签名以 `message, category, filename, lineno` 开头，供工具类代码
跳过调用栈推断直接定位。

### 过滤

过滤器是有序列表，按插入序匹配第一条命中的规则。动作全集为 `"default"`、`"error"`、`"ignore"`、
`"always"`（别名 `"all"`）、`"module"`、`"once"`。

```python
warnings.simplefilter("error", MyWarning)               # MyWarning 一律升级为异常
warnings.filterwarnings("ignore", message=r"^cache ")   # 消息以 "cache " 开头的忽略

warnings.resetwarnings()                                # 清空全部过滤器，回到默认策略
```

`filterwarnings` 的 `message` 与 `module` 是正则表达式，且只匹配开头。`message` 匹配忽略大小写，
`module` 区分大小写；要表达「包含」语义需自己写 `.*` 前缀。两个函数的 `category=None` 都归化为
`Warning` 基类，匹配全部警告。非法正则在插入时即报 `NotImplemented`。

动作语义：`"error"` 把警告升级为异常抛出，脚本可用 `try` 与 `except` 捕获；`"once"` 与 `"module"`
依赖去重注册表，同一警告只报一次。

### catch_warnings

```python
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    warnings.warn("captured", MyWarning)

if len(w) != 1:
    raise AssertionError("expected exactly one captured warning")   # 不用 assert：-O 下会被剥离
entry = w[0]
entry.message    # 警告消息
entry.category   # 类别
entry.filename / entry.lineno   # 归因位置
entry.file / entry.line / entry.source
```

`record=True` 时 `catch_warnings` 的 `__enter__` 返回收集列表，退出时自动恢复进入前的过滤器与状态。
每个 `catch_warnings` 进入时对当前状态独立拍照，因此嵌套是安全的，内层的修改不影响外层的恢复点。
同一对象二次进入报 `RuntimeError`。`module` 参数未实现，传入报 `NotImplemented`。

### @deprecated（PEP 702）

```python
from warnings import deprecated

@deprecated("use new_api instead")
def old_api(): ...

@deprecated("gone soon", category=DeprecationWarning)   # 内建名，无需 import
class Legacy: ...
```

`DeprecationWarning` 等警告类别都是内建名字，由 `builtins` 模块直供，Python 侧直接使用即可。

装饰后的类或函数在运行期被调用时发出警告。`category` 省略时默认为 `DeprecationWarning`；显式传
`category=None` 则只打标、不告警，保留 `__deprecated__` 元数据。另接受 `stacklevel` 关键字，
经 `__index__` 归化。

### 在宿主侧收集警告输出

警告沿错误流输出。把宿主的 `UseError` 接到独立的 `MemoryStream`，即可在 C# 侧收集警告文本，与
[捕获 Python 输出](./hosting.md#捕获-python-输出)是同一机制：

```csharp
var stderr = new MemoryStream();
var host = PyEnvironmentHost.CreateBuilder()
    .UseOut(Console.OpenStandardOutput())
    .UseError(stderr)                    // 警告文本落在这里
    .Build();
// 执行含 warnings.warn 的脚本后：
// Encoding.UTF8.GetString(stderr.ToArray())
//   → "prog.py:3: UserWarning: deprecated soon"
```

需要结构化的警告而非文本时，让脚本自己用 `catch_warnings(record=True)` 收集，再经约定通道传出。

## dataclasses

`@dataclass` 按类的注解字段自动生成方法，实现为纯 Python 的冻结模块，源码在编译期嵌入：

```python
from dataclasses import dataclass, field

@dataclass
class Point:
    x: int
    y: int = 0
    tags: list = field(default_factory=list)    # 可变默认值用 factory

p = Point(1, 2)
p == Point(1, 2)          # True，自动 __eq__
print(p)                  # Point(x=1, y=2, tags=[])，自动 __repr__
```

- 装饰器参数：`init`、`repr`、`eq` 控制各方法的生成；`match_args` 默认 `True`，未覆盖时生成
  `__match_args__`，`match` 类模式的位置匹配因此生效。`frozen=True` 与 `order=True` 尚未支持，
  传入即报 `TypeError`。
- `field(*, default=..., default_factory=..., init=True, repr=True, compare=True, hash=None,
  metadata=..., kw_only=...)` 提供字段级控制。`default` 与 `default_factory` 互斥，同时给出报
  `ValueError`。`metadata` 以只读视图呈现。
- 类级与字段级 `kw_only` 以及 `KW_ONLY` 标记可用，用于切换后续字段为仅关键字；关键字专属参数
  不参与非默认参数的顺序校验。
- `InitVar` 伪字段占据 `__init__` 参数，不写入实例，排除在 `repr` 与 `eq` 之外。
- `__post_init__` 由生成的 `__init__` 在尾部按 CPython 规则调用，并传入全部 InitVar 值。
- 继承字段按逆 MRO 合并，基类字段在前，子类重定义覆盖。
- 未覆盖：`asdict`、`astuple`、`replace`，以及 `frozen=True` 与 `order=True`。缺失项访问时报
  `AttributeError` 或 `TypeError`。

## 相关节点

[标准库模块覆盖](../python-compat/stdlib-modules.md) ·
[与 CPython 的差异](../python-compat/cpython-differences.md) ·
[执行 Python 代码](./executing-python.md) ·
[宿主与 I/O 重定向](./hosting.md#编码与标准流对象)
