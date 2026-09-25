# 性能与资源特征

本篇汇总已知的性能相关事实与有意为之的取舍，每条都链接到对应子系统篇目。仓库当前没有基准测试
项目（解决方案为库、控制台、测试、生成器与分析器，没有 benchmarks），因此本篇不含官方吞吐数字。
特征描述依据实现结构，数字待基准设施建立后补充，见[路线图](../contributing/roadmap.md)。

## 分配与复用机制

解释器在热路径上系统性地避免分配，以下机制不需要使用者做任何事：

- 帧内存有两种来源：普通函数帧的「快局部加操作数栈」整块内存优先从帧栈的块分配器
  （`PyObjectMemoryAllocator`，`FrameState.Alloc`）切分，超过块尺寸才退到 `ArrayPool.Rent`，
  帧退出即归还（`PyVariables.Dispose`）。见[调用与帧](./calls-and-frames.md)。
- 调用路径的复用缓冲：`PyCallContext.BuilderPool`（`ImmutableArrayBuilder` 对象池）组装参数；
  虚拟机状态里的 `CacheArgs`、`CacheKwargs`、`CachePairs`、`CacheBuilder` 在多次调用间复用列表、
  有序字典与字符串构建器。见 [Utility 速览](./utility-overview.md)与
  [生成器与协程系统](./generator-system.md)。
- `PyArguments` 是 `ref struct`，调用现场在栈上传递，零分配。
- 小整数池覆盖 `[-5, 256]`。字符串有三层驻留：256 项字符池、`PySpecialNames` 冻结字典与环境级
  并发字典，见[数值系统](./numeric-system.md)与[字符串系统](./string-system.md)。
- 源生成器消除运行时反射：类型注册在编译期产出，这同时是 AOT 与 trimming 兼容的前提，见
  [源生成器与代码分析](./source-generators.md)。

## 数据结构复杂度

- dict：读与插入摊还 O(1)（分离链址加素数扩容）；删除与 `popitem` 是 O(n)，因为条目搬移后要全量
  重建桶，源码标注 `TODO: perf`，是当前明确的优化方向。string 键走无分配的快速路径，见
  [dict 系统](./dict-system.md)。
- str 与 bytes：`.Value` 与底层数组直接承载，无编码转换层；驻留命中时构造零分配。
- 整数：`PyMath` 对 `int` 范围内的运算有专门快速路径，超出才进 `BigInteger` 通用路径，见
  [数值系统](./numeric-system.md)。

## 编译开销

- 没有字节码缓存：不产生也不读取 `.pyc`，每次 `import` 与每次 `Execute(源码)` 都完整走「词法、
  语法、语义、字节码」流水线。对反复执行同一脚本的场景，用会话复用摊薄：同一 `PyInterpreter`
  多次 `Execute` 共享 `__main__`，或用 `Execute(PyCodeObject)` 复用已编译的代码对象（Python
  侧 `compile()` 也可获得）。见[执行 Python 代码](../user-guide/executing-python.md)。
- 单次编译的开销集中在 parser（手写递归下降）与发射器，没有已测量的每 KB 编译耗时数据。

## AOT、trimming 与部署尺寸

- 主库在 Debug 与 Release 均开启 `IsTrimmable` 与 `IsAotCompatible`。扩展模型（attribute 加源
  生成器）不依赖运行时反射，AOT 发布不被阻断，见[总体架构](./architecture.md)。
- NuGet 包内嵌源生成器与分析器 DLL（`analyzers/dotnet/cs`），pack 前需先以同一配置构建解决
  方案，见[发布流程](../contributing/release-process.md)。

## 并发成本与边界

- 没有 GIL：多个 .NET 线程可并行进入解释器，但共享可变对象依赖各自的线程安全。`PyDictObject`
  是非线程安全的纯数组实现，跨线程共享需外部同步，见
  [threading 与 queue](./threading-and-queue.md)。
- Python 侧的 `threading.Thread` 映射为 .NET 托管线程。环境退出时以中断加 Join 收尾，长阻塞线程
  会拖延 `Dispose`。
- 每线程必须派生自己的 `PyCallContext`（帧栈私有），跨线程缓存上下文是错误用法。

## 如何自行测量

1. 使用 Release 配置（`dotnet build -c Release`）。Debug 下的块分配器与内联行为不代表真实性能。
2. 嵌入侧对比时注意控制变量：宿主 I/O 用 `Stream.Null` 或 `MemoryStream` 而非控制台；文件系统用
   内存 FS 避免磁盘噪声；会话复用方面，单环境多次 `Execute` 与每次新建环境的结果差异包含完整的
   环境初始化开销。
3. 想隔离编译开销时，对同一源码分别测 `Execute(string)`（含编译）与 `Execute(PyCodeObject)`
   （纯执行）。
4. 面向解释器内部优化时，贡献者侧的定位手段见[调试指南](../contributing/debugging.md)。

## 已知优化方向

- dict 删除与 `popitem` 的 O(n) 重建，见 [dict 系统](./dict-system.md)。
- 其余散落标记以源码的 `TODO: perf` 注释为准，聚合视图见
  [路线图](../contributing/roadmap.md)。

## 相关阅读

[dict 系统](./dict-system.md) · [调用与帧](./calls-and-frames.md) ·
[Utility 速览](./utility-overview.md) · [threading 与 queue](./threading-and-queue.md)
