# 生成器与协程系统

源码：`PySharp/Modules/Builtins/PyGeneratorObject.cs`、`PyCoroutineObject.cs`、
`PyAsyncGeneratorObject.cs`、`PyAnextAwaitableObject.cs`、
`PySharp/Runtime/VirtualMachine/BytecodeVirtualMachineStates.cs`。

生成器、协程与异步生成器共用同一套挂起与恢复机制：可挂起的执行状态（帧加 VM 状态）从虚拟机搬到
对象里，由对象上的协议方法重新驱动。本篇覆盖状态承载、与 VM 的交接以及三种身份的差异。

协程的 `await` 由 VM 同步驱动，生成器与协程对象层不存在 .NET Task 桥接。真正的 .NET 线程只在
`threading` 模块层出现。

## 一个值类，三种身份

```csharp
public abstract class PyGeneratorObject : PyObject, IPyObjectName
{
    internal abstract PyResult PyNext(PyCallContext context);
    internal abstract PyResult PySend(PyCallContext context, PyObject value);
    internal abstract PyResult PyThrow(PyCallContext context, PyObject value);
    internal abstract PyResult PyThrow(PyCallContext context, PyObject type, PyObject value, PyObject tb);   // 三参形态
    internal abstract PyResult PyClose(PyCallContext context);
}

public sealed class PyBytecodeGeneratorObject : PyGeneratorObject   // 唯一具体实现
{
    private bool IsGeneratorRunning;              // 首次 resume 后为 true
    private bool IsExecuting;                     // 重入守卫，CPython gi_running 语义
    private PyInternalFrame _frame;               // struct 字段
    private BytecodeVirtualMachineStates _vmStates;

    private bool IsCoroutine => _pyType is PyCoroutineObjectType;          // 身份由元类型决定
    private bool IsAsyncGenerator => _pyType is PyAsyncGeneratorObjectType;
}
```

三个元类型 `PyGeneratorObjectType`、`PyCoroutineObjectType`、`PyAsyncGeneratorObjectType` 都是
`PyTypeObject<PyGeneratorObject>`，值类型不分家，差异全部在元类型上：`Iter` 与 `Next` 对
`Await` 对 `AIter` 与 `ANext`，以及各自的方法集 `send`、`throw`、`close` 对 `asend`、`athrow`、
`aclose`。函数首次执行到 `ReturnGenerator` 指令时，按 `CodeObjectFlags`（`AsyncGenerator`、
`Coroutine`、皆无）选择元类型创建生成器对象并挂起。

## 状态承载

```csharp
internal struct BytecodeVirtualMachineStates
{
    internal PyExceptionObject? ExceptionToRaise;   // throw() 与 close() 的注入载体，恢复点消费
    internal bool RunToEnd;                         // 终结标志：帧执行到头，区别于挂起
    internal int OperandStackSize;                  // 挂起时的栈深
    internal Stack<ExceptionHandler> ExceptionHandlers;   // 帧内异常处理器栈，随对象搬移
    internal readonly OperandStack? Stack;          // 借用自 ArrayPool 的操作数栈
    internal Stack<PyExceptionObject> Exceptions;   // 正在处理的异常链，exc_info 语义
    internal List<PyObject> CacheArgs;              // 以下为调用路径的复用缓冲，避免每次调用分配
    internal OrderedDictionary<string, PyObject> CacheKwargs;
    internal List<KeyValuePair<PyObject, PyObject>> CachePairs;
    internal StringBuilder CacheBuilder;
}
```

`PyCore.Eval` 每次进入时新建一份 states，从 `ArrayPool` 按 `Bytecode.StackSize` 租借操作数栈。
`ReturnGenerator` 把它与帧一起拷贝进生成器对象，此后这段执行状态归对象所有。

## 挂起与恢复

挂起走「中途退出但不清理」的路径。`YieldValue` 弹出产出值赋给 `intermediateValue`，主循环在指令
分发后检查它，非空即带值跳到 `eval_end`。`eval_end` 的关键分叉是：

- `RunToEnd == true`（正常走完或未捕获异常）：`frame.Dispose` 并归还操作数栈，彻底结束。
- `RunToEnd == false`（`yield` 或首次 `ReturnGenerator`）：只把当前栈深存进 states
  （`states.Stack.Count = Stack.Count`），栈对象与异常处理器栈原样保留，这就是挂起。

恢复由 `PyBytecodeGeneratorObject.Send` 完成：

```csharp
IsGeneratorRunning = true;
using var withFrame = context.WithFrame(ref _frame, dispose: false);   // 把保存的帧换进上下文
_vmStates.SetYieldReceivedValue(value);     // 把 send 的值压到挂起的栈顶，成为 yield 表达式的取值
var result = BytecodeVirtualMachine.Eval(context, ref _vmStates);
_frame.InstructionIndex = context.CurrentInternalFrame.InstructionIndex;   // struct 帧：指令指针需回写
```

两个易错点：帧是 struct，`WithFrame` 交换进上下文的是副本，指令指针必须显式拷回；`RunToEnd` 之后
再 `send` 直接得到 `StopIteration`。

四个协议方法族的语义：`Next` 等价于 `Send(None)`；`Send` 首次传入非 `None` 报 `TypeError`；
`Throw` 接受类型或实例，支持 `(type, value, tb)` 三参形态（实例加非空 value 报 `TypeError`，
类加 value 以 value 实例化）；`Close` 注入 `GeneratorExit` 并吞没它，若生成器反而 `yield` 则报
`RuntimeError: ... ignored GeneratorExit`。

resume 前的状态守卫对齐 CPython 的 `gen_send_ex2` 与 `gen_throw`：

| 状态 | `send` 与 `next` | `throw` |
| --- | --- | --- |
| 未启动 | 正常启动；首次传入非 `None` 报 `TypeError` | 异常直接传播给调用者并关闭生成器，因为没有正在执行的帧可以捕获它 |
| 执行中 | `ValueError: generator already executing`，协程与异步生成器各有措辞 | 同左 |
| 已终结 | `StopIteration` | 传入的异常原样返回给调用者 |

`ExceptionToRaise` 的注入点在恢复路径上的 `_CheckExcToRaise` 指令处，VM 检查它非空即抛
`PyRuntimeException`。`throw()` 与 `close()` 就是这样在 yield 挂起点抛出的。

PEP 479 转换（`ConvertStopIteration`）：从生成器帧逃逸的 `StopIteration`（生成器体 `raise`，
或 `throw` 注入后未被捕获）不能被误判为正常耗尽，会被替换为
`RuntimeError: generator raised StopIteration`，原始 `StopIteration` 挂在 `__cause__` 上。协程与
异步生成器各有专属消息。正常 `return`（VM 层的 `RunToEnd`）不受影响。

异常作用域的还原：恢复共享调用者的上下文时，把调用者进入时观察到的 handled exception 在挂起与
退出时还原，因此生成器内恢复后执行裸 `raise` 看到的是调用者当时的活动异常，而不是残留的过期异常。
这配合[虚拟机](./virtual-machine.md)的裸 `raise` 动态作用域实现。

## await 的驱动

`GetAwaitable` 指令对协程直通（已在 await 目标位），否则调用 `__await__` 协议，见
[协议分发](./protocol-dispatch.md)。`GetAIter` 调 `__aiter__`，并按 CPython 3.14 语义校验结果有
`__anext__`。随后的 `Send` 指令进入 `InternalSend`（`BytecodeVirtualMachine.BytecodeImpl.cs`）：

- `yield from` 双向代理：栈上是子迭代器与待发值。注入异常时优先委托子生成器的 `throw` 或
  `close`，无该方法才在自身抛出；正常驱动时对 `PyGeneratorObject` 走 `PyNext` 与 `PySend` 快速
  路径，其他对象走 `__next__` 与 `send()`。
- 完成值的上浮：子迭代器以 `StopIteration` 结束时，其 `return value`（异常的 `args[0]`）替换栈上
  的待发值，并跳转到 `instructionArg` 继续。这是 `yield from` 与 `await` 拿到结果的方式。
- 驱动是同步的：循环调用直到 `StopIteration` 或异常，没有事件循环与 Task。

## 异步生成器对象层

`__anext__()`、`asend()`、`athrow()` 返回 `PyAsyncGeneratorASendObject`，对应 CPython 的
`async_generator_asend`：

- `__await__` 与 `__iter__` 返回自身，它自己就是被 `Send` 指令驱动的迭代器。
- `Next` 驱动底层生成器并包装结果：成功产出转换为 `StopIteration(value)`，向上层 `Send` 发完成
  信号；自身收到 `StopIteration`（生成器耗尽）时转换为 `StopAsyncIteration`；其他错误透传。
- `athrow` 模式经 `CreateForThrow` 构造，首次驱动时注入异常。

`anext()` 内建函数的默认值参数由 `PyAnextAwaitableObject`（internal）支撑：`__await__` 时调用
迭代器的 `__anext__` 并存为内部可等待对象，返回自身，捕获 `StopAsyncIteration` 后换出默认值。

## 贡献者提示

- 改挂起与恢复路径后，必须覆盖的回归族包括 `test_yield.py`、`test_async_for.py`、
  `test_async_generator.py`、`test_async_control_flow_regression.py`、
  `test_for_return_cleanup.py`（finally 清理与生成器的交互）。未启动 `throw` 的守卫矩阵也要覆盖：
  实例、类、三参、`StopIteration` 与 `GeneratorExit` 传播，关闭后的后续操作，耗尽与挂起对照，
  `genexpr` 同面。
- 新增可等待对象的正确姿势是实现 `Await` 槽并返回可被 `Send` 驱动的迭代器，而不是引入 .NET
  异步原语。对象层与 VM 的契约是迭代器协议，应保持同步驱动语义。
- `states.Cache*` 缓冲被多帧复用，改动调用指令（`Call`、`CallKw`、`CallFunctionEx`）时注意不要
  跨帧持有。

## 相关阅读

[虚拟机](./virtual-machine.md)（主循环与 `eval_end` 分叉）· [调用与帧](./calls-and-frames.md)
（帧生命周期）· [字节码](./bytecode/README.md)（`ReturnGenerator`、`YieldValue`、`Send` 指令族）
