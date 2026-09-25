# threading 与 queue

源码：`PySharp/Modules/Threading/`（`PyThreadObject.cs` 与 `.Py.cs`）、
`PySharp/Modules/Queue/`、`PySharp/Runtime/Environments/PyEnvironment.cs`（收尾）、
`PySharp/Runtime/Calls/PyCallContext.cs`（`FromCreatingThread`）。

这两个模块是对象层仅有的 .NET 并发原语使用点，协程不走线程，见
[生成器与协程系统](./generator-system.md)。核心问题有两个：Python 线程如何映射到托管线程并获得
自己的执行上下文，以及进程退出时如何不泄漏地收尾。

## threading

### 线程的创建与启动

`Thread(target, args, kwargs)` 只持有目标，`start()` 才创建 .NET 线程，重复 `start` 抛
`InvalidOperationException`。启动逻辑在 `PyThreadObject.Py.cs`：

```csharp
_thread = new Thread(() =>
{
    using var threadContext = PyCallContext.FromCreatingThread(context);   // 1
    try { PyInterpreter.PyTryCatch(threadContext, () => PyRun(threadContext)); }
    catch (ThreadInterruptedException) { /* 环境收尾的中断，静默退出 */ }      // 2
    context.PyEnvironment.Threads.Remove(_thread);                         // 3
});
context.PyEnvironment.Threads.Add(_thread);                                // 4 登记先于 Start
_thread.Start();
```

1. 上下文派生：`PyCallContext.FromCreatingThread` 从创建线程的上下文派生，用
   `CreateThreadRootFrame()` 挂新的线程根帧。它共享同一个 `PyEnvironment`，因此 globals、模块
   缓存与 Intern 池都跨线程可见，而线程内异常的 traceback 与帧状态彼此独立。
2. 异常边界：`PyRun` 调用 target，错误结果转 `PyRuntimeException` 抛出，由 `PyTryCatch` 按
   「打印 traceback 到环境错误流」的常规路径处理，见
   [执行 Python 代码](../user-guide/executing-python.md)。
3. 登记与注销：线程加入 `PyEnvironment.Threads`（`ConcurrentSet<Thread>`）后启动，正常退出时
   自我移除。

`join(timeout=None)` 对应 `_thread.Join()` 或 `Join(TimeSpan.FromSeconds(timeout))`，负超时截为
0；`is_alive()` 对应 `_thread.IsAlive`。`daemon` 与 `context` 构造参数目前仅被接受，未实现差异化
行为。

### 环境收尾

`PyEnvironment.Dispose` 对线程集中每个线程先 `Interrupt()` 再 `Join()`，见
[配置执行环境](../user-guide/environment.md)：

- `Interrupt` 触发线程内等待原语抛 `ThreadInterruptedException`，`PyStart` 的 catch 把它吞掉，
  线程带着未完成的帧退出。帧清理尚未完善的遗留点记录在源码的 `EnsureFrameState` 处。
- `Interrupt` 可能失败（线程已过等待点），此时与 CPython 一样选择等待而非放弃。
- `PyInterpreter.RunRepl` 在循环内自行处理异常并继续会话。`sys.exit` 的退出码记入环境。

## queue

`PyQueueObject`（`queue.Queue`）的底座是 `ConcurrentQueue<PyObject>` 加
`BlockingCollection<PyObject>`，`maxsize <= 0` 时无界，否则有界。方法族的映射：

| Python 语义 | 实现 |
| --- | --- |
| `put(block=True, timeout=None)` | `IsAddingCompleted` 时抛 `Shutdown`；阻塞用 `Add`，限时用 `TryAdd(ms)` 失败抛 `Full`；非阻塞 `TryAdd` 失败抛 `Full` |
| `get(...)` | `IsCompleted` 时抛 `Shutdown`；阻塞用 `Take`，限时 `TryTake(ms)` 失败抛 `Empty` |
| `qsize`、`empty`、`full` | `_queue.Count`、`IsEmpty`、`Count == BoundedCapacity` |
| `task_done()` | 见下 |
| `join()` | 见下 |

未完成任务计数是 `join()` 语义的核心：

- 每次 `put` 成功都 `Interlocked.Increment(_unfinished_tasks)`。
- `task_done()` 执行 `Interlocked.Decrement`。减到负数即回滚并报错，用于捕获比 `join` 多调的
  `task_done`；减到 0 时 `_source.TrySetResult()` 唤醒等待者并清空 `_source`。
- `join()` 在计数为 0 时直接通过，否则在 `_sourceLock` 内惰性创建 `TaskCompletionSource`，以
  防止「创建与等待之间恰好 `task_done`」的竞态（已有其他线程在 `join` 时复用同一个），然后
  `source.Task.Wait()`。

因此 `join` 的完成信号是一次性的 `TaskCompletionSource` 广播，`put` 与 `task_done` 的计数全程
原子操作。`Shutdown` 类型对应 CPython 3.13 的 `queue.ShutDown`。

## 贡献者提示

- 线程代码里不要缓存跨线程的 `PyCallContext`，每个线程必须经 `FromCreatingThread` 派生自己的
  上下文，因为帧栈是上下文私有的。
- 环境收尾依赖线程集的登记纪律。新的会阻塞的运行时设施要么接入 `Threads` 收尾，要么确保
  `Interrupt` 能打断其等待原语。
- 没有 GIL。跨线程共享的 Python 对象依赖各自实现的线程安全，例如 `PyStrObject.InternPool` 是
  环境级并发字典，queue 使用 `BlockingCollection`。`PyDictObject` 是非线程安全的纯数组哈希桶，
  跨线程共享 dict 需外部同步，见 [dict 系统](./dict-system.md)。新增可变内建类型时要明确其并发
  策略。
- 回归覆盖在 `test_queue.py`（含阻塞、超时与 `task_done` 次数校验）与 threading 相关语料。

## 相关阅读

[Environments 与模块解析](./environments-and-modules.md)（线程集与收尾的所有者）·
[调用与帧](./calls-and-frames.md)（`FromCreatingThread` 与帧栈）·
[生成器与协程系统](./generator-system.md)（协程不走线程的对照）
