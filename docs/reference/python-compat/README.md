# Python 兼容性

本分区回答一个问题：手里的 Python 脚本或依赖，在 PySharp 上能不能跑。三篇互为索引，
建议按下面的流程评估。

## 评估流程

1. 用到的语法特性是否在[语言特性支持清单](./language-features.md)内？清单外的特性可能部分可用，
   但未经系统验证。
2. 依赖的标准库模块是否在[标准库模块覆盖](./stdlib-modules.md)的 13 个内嵌模块内，函数级覆盖
   是否足够？
3. 是否触发[与 CPython 的差异](./cpython-differences.md)中的某一条？逐字对照 CPython 输出的测试、
   依赖具体 `hash` 值的场景、多进程模块等，可以直接排除。

## 篇目索引

| 篇目 | 内容 | 依据 |
| --- | --- | --- |
| [语言特性支持清单](./language-features.md) | 按类别（基础语法、函数、类与 OOP、异常、迭代与生成器、异步、流程控制、数据模型细节）的支持矩阵 | `test_pyfiles` 中 387 个回归脚本覆盖的主题 |
| [内建类型方法面覆盖](./builtin-type-methods.md) | `str`、`bytes`、`list`、`dict`、`set` 等内建类型已实现的方法与属性清单，并列出 CPython 有而未支持的高频成员 | 各类型 `[PyMethod]` 等声明的核对 |
| [标准库模块覆盖](./stdlib-modules.md) | 13 个内嵌模块逐个列出常量、函数与方法，43 个内建函数清单，`open()` 与 import 的文件系统支撑 | 各模块 `[PyExport]` 与 `[PyMethod]` 声明 |
| [与 CPython 的差异](./cpython-differences.md) | 标准库面、运行时细节（错误消息、`hash`、`id`、线程、GC）、嵌入 API 边界、工具链（REPL、CLI）四组差异与限制 | 实现核对 |

## 维护约定

这几篇是对外承诺面。改动语言、标准库或嵌入 API 行为的提交应同步更新对应篇目，见
[路线图](../contributing/roadmap.md)的文档同步义务一节。行为变更的验收以 `test_pyfiles` 回归为准，
见[测试体系](../internals/testing.md)。
