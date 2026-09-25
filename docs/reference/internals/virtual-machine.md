# 虚拟机

源码：`PySharp/Runtime/VirtualMachine/`、`PySharp/Runtime/PyCore.cs`。

执行核心是栈式字节码解释器，主循环是对 `OpCode` 的大型 `switch` 分发。

## 文件一览

| 文件 | 职责 |
| --- | --- |
| `PyCore.cs` | 执行编排：`Eval(context)`、`MakeFunction`、`GetFreeVars`、`BuildClass` |
| `BytecodeVirtualMachine.cs`（1307 行） | `Eval(PyCallContext, ref BytecodeVirtualMachineStates)` 主循环与大部分操作码实现 |
| `BytecodeVirtualMachine.BytecodeImpl.cs`（693 行） | 一部分操作码的独立实现，复杂指令的分解 |
| `BytecodeVirtualMachineStates.cs` | 可挂起的执行状态，供生成器与协程恢复：借用栈、操作数栈深、异常处理器栈等 |
| `OperandStack.cs` | 操作数栈：池化数组 `OperandStack` 加 `ref struct` 视图 `ValueOperandStack`。`Pop` 下溢与 `Push` 上溢都有保护，即 `Debug.Assert` 加受控异常 |

## 主循环结构

```csharp
internal static PyResult Eval(PyCallContext context, ref BytecodeVirtualMachineStates states)
{
    ref var frame = ref context.CurrentInternalFrame;   // 当前帧：指令索引、变量槽、code 对象
    ...
    eval_begin:
        var instructions = frame.CodeObject.Bytecode.Instructions.AsSpan();
        ...
    eval_resume:
        while (currentIndex < length)
        {
            instructionArg |= instruction.Arg;          // ExtendedArg 字节累积
            switch (instruction.OpCode) { ... }
        }
}
```

要点：

- 指令与常量池缓存到局部变量与 span：`instructions`、`consts`、`names` 每轮取 `AsSpan()`。
  `ExtendedArg` 以 `instructionArg <<= 8` 累积，由下一条真实指令消费。
- 操作数栈是帧内 span：`ValueOperandStack` 直接视图在 `frame.Variables.OperandStackSpan` 上，
  容量来自 `Bytecode.StackSize` 的静态分析。内联帧（推导式）共享外围非内联帧的栈，因为列表与
  迭代器本就活在宿主帧的栈位上，`CreateInline` 不另配栈。
- 寄存器式局部缓存：`value`、`left`、`right`、`result` 等 `PyObject` 局部变量充当寄存器，
  减少栈的弹压。
- 调用深度与 `evalResult`：函数调用经 `needCheckEvalResult` 把被调帧的结果压回本帧栈顶，
  避免递归 C# 调用。生成器挂起时直接带着 `PyResult` 退出循环。

## 异常处理模型

帧内维护 `ExceptionHandler` 栈：

```csharp
internal sealed class ExceptionHandler
{
    public int ExceptOffset, FinallyOffset;    // except 与 finally 入口的指令偏移
    public int State;                          // Init → Except → Finally → End
    public PyExceptionObject? PyException;     // 正在处理的异常
    public int StackDepth;                     // 进入时的栈深，用于回滚
    public PyObject? ReturnValue;
    public bool HitExcept;                     // 是否命中过 except，用于 finally 语义分支
    public int FrameIndex;
}
```

`try` 编译为 `_SetupExcept` 与 `_SetupFinally` 家族指令。VM 捕获到 `PyRuntimeException` 时不直接
上抛，而是查处理器栈：回滚操作数栈到 `StackDepth`，按状态机推进，`except` 匹配经 `CheckExcMatch`
与 `CheckEgMatch`（异常组支持子集匹配），全部处理完才把剩余异常作为 `PyResult` 错误离开 `Eval`。
`_LoadExcInfo` 与 `_LoadHitExcept` 支撑 `sys.exc_info` 族语义。

以下三处清理语义在改动控制流路径前需要注意，均有回归覆盖：

- `except ... as name` 的隐式清理：except 块结束时 VM 自动删除绑定的名字，避免异常对象因局部名
  绑定在帧内多活一轮 GC。
- `with` 多项异常清理的栈恢复：`__exit__` 自身抛异常且原有异常尚在传播时，两个异常按 CPython
  顺序合链；多个嵌套 `with` 逐层清理时，处理器栈与操作数栈的回滚深度必须逐项还原。
- 裸 `raise` 的动态异常作用域：裸 `raise` 读取的是运行时上下文的当前活动异常，经帧链的
  `exc_info` 状态，而不是编译期静态确定的处理器槽。生成器恢复路径上要把调用者的活动异常还原进
  上下文，见[生成器与协程系统](./generator-system.md)。

## 生成器与协程

- `ReturnGenerator` 让函数首帧立即以生成器对象返回，`PyGeneratorObject` 持有帧与
  `BytecodeVirtualMachineStates`。
- 挂起：`YieldValue` 把当前执行状态（借用栈、栈深、指令索引、处理器栈）打包进 states，并带值
  退出循环。恢复：重新进入 `Eval`，走 `eval_resume` 续跑。
- 状态守卫：生成器对象层在 resume 前检查未启动、执行中与已终结三态。重入此前会损坏帧并导致进程
  崩溃。未启动的 `throw()` 注入路径在 VM 侧跳过注入，直通调用者。守卫矩阵见
  [生成器与协程系统](./generator-system.md)。
- 异步侧：`GetAwaitable`、`GetAIter`、`GetANext`、`Send` 组合出 `await`、`async for` 与异步
  生成器协议，.NET Task 的桥接在对象层完成。

## PyCore 的编排职责

- `Eval` 是 `PyInterpreter.Execute`、函数调用与模块执行共用的唯一执行入口。
- `MakeFunction`：以 `PyCodeObject`、`PyArgsDef`（签名）与自由变量单元构造 `PyFunctionObject`，
  帧的 globals 一并传入，`__module__` 在构造时快照全局 `__name__`，见
  [调用与帧](./calls-and-frames.md)。
- `GetFreeVars`：按 `FreeVars` 从外围帧收集 `PyCellObject`，函数与类两种来源。
- `BuildClass`：类体执行完毕后创建类型，解析 `metaclass` 关键字参数与基类元类冲突并选出最派生
  元类，应用命名空间与类单元，落成 `PyTypeObject`。用户定义类走
  `CreateUserDefinedTypeWithSameLayout` 布局。

## 相关阅读

- [字节码](./bytecode/README.md)、[调用与帧](./calls-and-frames.md)、[对象模型](./object-model.md)
- [生成器与协程系统](./generator-system.md)：`eval_end` 挂起分叉的消费方、`Send` 指令
- [异常组](./exception-groups.md)：`ExceptionHandler` 状态机在 `except*` 上的应用
