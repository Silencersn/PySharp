# 实现内幕

本分区面向贡献者与研究者，覆盖解释器的全部子系统。入口是[总体架构](./architecture.md)，
其中包含数据流全景与阅读路线。下面按子系统分组给出索引。

## 架构与编译前端

按「源码到字节码」的顺序：

| 篇目 | 内容 |
| --- | --- |
| [总体架构](./architecture.md) | 数据流全景、目录职责表、关键设计决策、推荐阅读路线 |
| [词法分析](./tokenization.md) | `Lexer` 状态机、缩进栈、f-string 嵌套栈、token 匹配 |
| [语法分析](./parsing.md) | 手写递归下降分析器的分部组织、AST 形态、名称改写、REPL 容错协作 |
| [语义分析](./semantic-analysis.md) | 变量存储分类（快局部、cell、free、全局）、`VariableScope` 树、闭包分析 |
| [字节码](./bytecode/README.md) | 指令格式、标签回填与窥孔优化、编译期字面量折叠，及八篇家族指令参考与反汇编走查 |
| [f-string 与格式化](./fstring-and-format.md) | 格式化指令族、t-string 对象、`PyFormatSpec` 迷你语言 |

## 虚拟机与执行环境

| 篇目 | 内容 |
| --- | --- |
| [虚拟机](./virtual-machine.md) | 主循环结构、操作数栈、`ExceptionHandler` 状态机、`PyCore` 编排 |
| [生成器与协程系统](./generator-system.md) | 挂起与恢复状态的承载、三种身份、`Send` 指令 |
| [调用与帧](./calls-and-frames.md) | `PyCallContext`、`PyArguments`、`PyArgsDef`、帧形态、traceback 捕获 |
| [Environments 与模块解析](./environments-and-modules.md) | 对象关系、import 解析流程、提供器链、相对导入 |
| [异常组](./exception-groups.md) | PEP 654 的子集匹配、`split` 算法、`except*` 编译形态、rest 替换语义 |
| [threading 与 queue](./threading-and-queue.md) | 线程桥接与上下文派生、环境收尾协议、`BlockingCollection` 与 join 信号 |

## 类型系统

| 篇目 | 内容 |
| --- | --- |
| [对象模型](./object-model.md) | 类型对与 partial 拆分、slots、MRO、属性机制、描述符 |
| [协议分发](./protocol-dispatch.md) | `PySpecialMethods` 与 `PyOperators` 的回退规则、反射协议、比较器族 |
| [字符串系统](./string-system.md) | `PyStrObject` 的驻留层、`PyStrConverter`、方法注册机制 |
| [数值系统](./numeric-system.md) | 任意精度与缓存、`PyMath` 快速路径、`PyHash` 归一化 |
| [dict 系统](./dict-system.md) | 自研哈希桶、string 键快速路径、属性与 locals 字典接口分层、帧 locals 代理 |
| [用户类创建全流程](./class-creation.md) | `class` 语句从发射到 `type.__new__` 的链路、布局决议 |

## 工具链与质量

| 篇目 | 内容 |
| --- | --- |
| [源生成器与代码分析](./source-generators.md) | 生成器的产物、`PYARG`、`PYSP`、`PYSPI` 规则表、自举验证 |
| [测试体系](./testing.md) | 「断言写在 Python 里」的策略、语料命名惯例、新增测试流程 |
| [Utility 速览](./utility-overview.md) | 与 Python 语义无关的工具类清单、准入标准 |
| [性能与资源特征](./performance.md) | 分配复用机制、数据结构复杂度、编译开销、AOT 与并发边界、测量方法 |

动手改代码前，配合 [contributing](../contributing/README.md) 的操作指南使用。
