# 调用与帧

源码：`PySharp/Runtime/Calls/`、`PySharp/Runtime/PyInternalFrame.cs`。

调用子系统连接协议层的可调用对象与虚拟机的帧执行，负责参数的收集与校验、帧的创建与回收、
执行上下文的持有。

## PyCallContext

```csharp
public sealed partial class PyCallContext : IDisposable
{
    internal PyEnvironment PyEnvironment { get; }
    internal PyCallContextFrameState FrameState { get; }   // 帧栈
    public PyObjectComparer Comparer { get; }              // 绑定上下文的比较器
    internal StreamReader In / StreamWriter Out / StreamWriter Error;   // 转发环境 I/O
    internal ref PyInternalFrame CurrentInternalFrame => ref FrameState.CurrentInternalFrame;
}
```

- 唯一入口是环境。全部工厂（`CreateInterpreterRootContext`、`CreateFromEnvironment`、
  `FromCreatingThread`）为 `internal`。几个静态占位上下文（`CSharpRuntime` 等）服务库内非帧
  场景，如 `ToString` 求 repr。这是「外部拿不到上下文」这一公共 API 边界的根源，见
  [与 CPython 的差异](../python-compat/cpython-differences.md)。
- 帧栈：`PyCallContextFrameState` 维护 `PyInternalFrame` 栈（`EnterFrame`、
  `ExitInternalFrame`）。`WithFrame(ref frame)` 返回 `FrameSetter`，即 `ref struct` 加
  `IDisposable`，用 `using` 管理帧生命周期。
- 分部文件：`.Exceptions.Throwable`（把 `PyExceptionResult` 转成带定位的 `PyRuntimeException`
  并抛出）、`.Warnings`（警告通道）、`FrameState.Allocator`（帧与栈空间的池化分配）。

## PyArguments 与 PyArgsDef

```csharp
public readonly ref struct PyArguments      // 调用现场：位置参数、关键字参数、*args 与 **kwargs 收集
{
    public readonly ReadOnlySpan<PyObject> Args;
    public IReadOnlyList<PyObject> ExtraArgs { get; }             // *args 溢出
    public IReadOnlyList<KeyValuePair<string, PyObject>> ExtraKwargs { get; }
    public PyObject this[int index] { get; }                      // 按形参序号取绑定值
    public PyObject this[string key] { get; }                     // 按形参名取
}

public sealed class PyArgsDef { ... }       // 签名：形参名、默认值、仅位置、仅关键字、**kwargs 声明
```

- 签名串来自 `[PyFunctionParameters("item", "block=True", "timeout=None")]`，或 `def` 语句的
  解析结果。
- `PyArgsValidator` 按 `PyArgsDef` 校验 `PyArguments`，产生 CPython 风格的缺参、多参与未知关键字
  `TypeError`。
- 自定义类型与模块方法实现中的 `arguments[0]` 就是「第一个形参的绑定值」，校验已由包装层完成。
  这是[扩展指南](../user-guide/custom-types.md)中签名约定的底层机制。
- `PyArguments` 为 `ref struct`，在栈上传递，零分配。`PyArgsDef` 持有可选的池化 `Buffer`。

## PyInternalFrame

```csharp
internal partial struct PyInternalFrame
{
    internal PyVariables Variables;        // 变量槽：快局部 span、cell 与 free、globals、操作数栈
    internal PyObject? Caller;             // 调用者
    internal PyCodeObject? CodeObject;     // 正在执行的代码
    internal FrameType FrameType;          // Module / Function / ClassBuild / Comprehension / ExecEval / ThreadRoot 等
    internal int InstructionIndex;         // 指令指针，挂起与恢复的锚点
}
```

工厂方法覆盖全部帧形态：`CreateModuleFrame`（模块与根帧）、`CreateFuncCallFrame`（函数调用，
经 `InitArgs(def, code, arguments, closure)` 绑定参数）、`CreateClassBuildFrame`（类体）、
`CreateAnnotateFrame`、`CreateInlineFrame`（推导式，共享外围栈）、`CreateExecEvalFrame`
（`exec` 与 `eval`）、`CreateThreadRootFrame`（Python 线程）。`PyVariables` 把语义分析的分类
（`VarNames`、`CellVars`、`FreeVars`）落地为具体存储，`PyCellObject` 承载闭包单元。

`exec` 与 `eval` 帧的局部作用域对齐 CPython 的 `builtin_eval` 与 `exec_impl`：

- `globals` 与 `locals` 都省略时，locals 默认取调用帧的 locals。优化帧（快局部 span）用
  `GetLocals()` 的快照，非优化帧（类命名空间）用活映射。只有 `globals` 显式传入时，locals 才
  默认等于 globals。
- `FindOuterNonInlineFrame()` 支撑内联推导式帧（PEP 709）的判定：内联帧的快局部操作打在外围函数
  的 span 上，活 locals 属于 owner；类作用域下的推导式帧自持名字。
- `eval` 与 `exec` 对优化帧快局部的隐式写入被隔离，不落回调用帧的 span，显式 `global` 声明仍写
  真实全局作用域。
- `PyFrameLocalsProxyObject` 采用双层查找：`_parentLocals`（外围命名空间）只参与名字查找，
  枚举始终只含本作用域自己的名字，与 CPython 独立推导式函数看到的 locals 视野一致。

`PyVariables` 内部的字典分层在 [dict 系统](./dict-system.md)中展开。globals 恒为 `PyDictObject`；
locals 有无 locals、快槽与 `IPyVariablesLocalsDict` 慢路径三形态；`exec` 与 `eval` 传入的映射经
`PyFrameLocalsProxyObject` 桥接。

## 函数对象的元数据

`PyCore.MakeFunction(ref frame, codeObject, def)` 把帧的 globals 一并传入构造，用于元数据快照。
三个名字属性的读写语义对齐 CPython 的 `func_new` 与 `func_getset` 族：

- `__module__`：创建时从传入的 globals 快照 `__name__`，不存在则不设。之后全局 `__name__` 的
  变化不影响已创建的函数。这是 `T_OBJECT` 成员语义，任意值可写，删除后读回 `None`。
- `__qualname__`：惰性取代码对象的限定名（`Code.QualName`，由语义模型按词法栈组装，即嵌套函数
  名链加 `<locals>` 段，见[类创建](./class-creation.md)）。getset 语义为仅可赋 `str`，否则报
  `TypeError: __qualname__ must be set to a string object`；删除同样拒绝。
- `__name__`：可运行时赋值改名。

生成器、协程与异步生成器对象带同一套 `__name__` 与 `__qualname__` getset，有相同的 str 校验与
删除报文。

## traceback

异常在帧间传播时**逐帧累积**：解释器的错误出口（`BytecodeVirtualMachine.Eval` 的
`catch (PyRuntimeException)`）对当前帧调用 `PyExceptionObject.RecordFrame`，把该帧的
`CodeObject` 与 `InstructionIndex` 经 `LineTable` 反查出的源码行建为新节点，旧链挂在它的
`tb_next` 上（对应 CPython `PyTraceBack_Here`）。因此异常不会在输出边界被活跃栈快照覆盖。链条
用 Python 可见的 `PyTracebackObject`（类型名 `traceback`）表示，异常通过 `__traceback__` 暴露它：
头节点是最外层帧，沿 `tb_next` 走向异常发生处，即 "most recent call last" 的打印顺序。属性可读
可写（`None` 清空、traceback 替换），删除抛 `TypeError`，`BaseException.with_traceback` 转调该
setter 并返回 `self`。

两条路径不记录：`RERAISE` 语义的抛出（裸 `raise`、`finally` 收尾重抛）直接进入异常展开，异常
保留既有 traceback；显式 `raise e` 保留旧 traceback 并前插本次 raise 站点（对应 CPython
`do_raise`）。线程栈头（`Exception in thread ...`）存于异常而非 traceback 对象，故
`__traceback__` 被重新赋值也不影响它。最终由 `ToMessage`（即 `PyRuntimeException.Message`）
格式化为 Python 风格文本，见 [错误处理](../user-guide/error-handling.md)。

`PyTracebackObject` 目前只暴露 `tb_next` 与 `tb_lineno`：`tb_frame` 需要尚不存在的帧对象类型、
`tb_lasti` 的字节偏移语义与 PySharp 的字节码索引不可比，两者为已知缺口。

## 相关阅读

[虚拟机](./virtual-machine.md)（帧的消费者）· [语义分析](./semantic-analysis.md)（变量分类的
来源）· [PyResult 参考](../api/PyResult.md)
