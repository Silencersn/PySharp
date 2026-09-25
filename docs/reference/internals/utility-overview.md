# Utility 速览

源码：`PySharp/Utility/`，共 10 个文件、约 1020 行。

`Utility/` 收纳与 Python 语义无关的 .NET 基础设施：不做协议分发、不查槽、不产生 Python 错误。
它们可由 `PySharp.Tests/UtilityTests.cs` 独立测试，不经解释器。以下按用途分组。

## 池化与租借

| 类（文件） | 职责 | 主要使用方 |
| --- | --- | --- |
| `PoolHelper` | `ArrayPool<T>` 租借助手：`Rent(length, out arrayToReturn)` 返回定长 `Span`；`RentedArray<T>`（`ref struct`，`Dispose` 归还）；`ReturnIfNonNull` | 字节码栈深模拟（`Bytecode.StackSizeHelper`）等一次性 span 场景 |
| `ImmutableArrayBuilderPool`（同文件还含 `ObjectPool<T>`） | `ImmutableArray.Builder` 的对象池；`ImmutableArrayBuilderOf<T>`（`readonly ref struct`）按 `T` 是否值类型在 `ImmutableArray<T>.Builder` 与 `ImmutableArray<object>.Builder` 间分派，避免值类型装箱 | `PyCallContext.BuilderPool`，即调用路径组装参数数组的复用缓冲，见[调用与帧](./calls-and-frames.md) |
| `ArrayStackHelper` | 「数组即栈」的 `Push(ref array, ref length, item)`，满则容量翻倍（初始 4）；`PushPooled` 变体扩容走 `ArrayPool` | 手写栈结构 |

## 零分配容器与 span 工具

| 类 | 职责 |
| --- | --- |
| `InlinePyObjectArray` | `[InlineArray(8)]` 的 struct，在栈上内联 8 个 `PyObject` 字段，充当零分配小数组 |
| `SpanExtensions` | 两个低层工具：`ContainsRef<T>(span, ref value)` 用 `Unsafe.ByteOffset` 判定引用是否指向 span 范围内；`Cast<TFrom,TTo>` 对同尺寸 span 做 `Unsafe.As` 重解释，带 `Debug.Assert` 尺寸校验 |

## 并发

| 类 | 职责 |
| --- | --- |
| `ConcurrentSet<T>` | `ConcurrentDictionary<T, byte>` 包装的并发集合（`Add`、`Remove`、`Contains`、`Clear`、枚举）。它是 `PyEnvironment.Threads` 的类型，线程登记与收尾依赖它，见[threading 与 queue](./threading-and-queue.md) |

## 渲染与编码注册

| 类 | 职责 |
| --- | --- |
| `IndentedStringBuilder` | 缩进感知的 `StringBuilder`（`IncrementIndent`、`DecrementIndent`，新行自动补缩进）。用于 traceback 与异常组子异常树的渲染，见[错误处理](../user-guide/error-handling.md) |
| `CodePagesEncoding` | `EnsureRegistered()` 把 .NET 代码页编码注册进 `Encoding.GetEncoding` 的解析面。源码字节解码（PEP 263 编码声明可指定 `gbk` 等非 UTF 系编解码器）依赖它，见[词法分析](./tokenization.md) |

## 运行时配置状态

| 类 | 职责 |
| --- | --- |
| `PyIntStrDigitsLimit` | 整数与字符串转换的位数上限（默认 4300，最小 640，0 禁用）的进程级持有者，经 `sys.set_int_max_str_digits()` 暴露。语义与消费方见[数值系统](./numeric-system.md) |

历史上的 `DictAdapter`（把 .NET 字符串字典适配成 Python 字典视图）已随 dict 重写删除，
`PyDictObject` 本身成为自研哈希桶实现，见 [dict 系统](./dict-system.md)。

`BigIntegerHelper`（进制解析与进制输出）在[数值系统](./numeric-system.md)中已有完整说明。

## 约定

- 准入标准：新工具进 `Utility/` 的条件是不含任何 Python 语义，即不依赖协议、不抛 Python 异常、
  不需要 `PyCallContext`。否则放到对应的子系统目录。
- 可见性：除 `IndentedStringBuilder` 与 `ObjectPool<T>` 为 public 外均为 `internal`，它们是库的
  实现细节，见[公共 API 稳定性政策](../contributing/api-stability.md)。
- 测试：由 `UtilityTests.cs` 直接覆盖，不经解释器，新增工具类时应顺手补测试，见
  [测试体系](./testing.md)。

## 相关阅读

[调用与帧](./calls-and-frames.md) · [threading 与 queue](./threading-and-queue.md) ·
[数值系统](./numeric-system.md) · [字符串系统](./string-system.md) · [dict 系统](./dict-system.md)
