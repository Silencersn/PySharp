# PyInterpreter 参考

源码：`PySharp/Runtime/PyInterpreter.cs`。命名空间：`PySharp.Runtime`。

解释器入口，`sealed class`，实现 `IDisposable`。分为静态便捷方法（每次自建环境）与实例方法
（在受控环境上执行）两层。使用指南见[执行 Python 代码](../user-guide/executing-python.md)。

## 静态方法

| 成员 | 说明 |
| --- | --- |
| `static PyModuleObject RunFile(string filename, IEnumerable<string>? args = null)` | 读取物理文件执行。`sys.argv[0]` 为 `filename`，脚本所在目录加入 `sys.path`；使用物理文件系统的控制台宿主 |
| `static PyModuleObject? RunCode(string code, string? moduleName = null, string? sourceName = null, IEnumerable<string>? args = null)` | 执行源码字符串。默认 `moduleName` 为 `"<module>"`、`sourceName` 为 `"<string>"`；`sys.argv[0]` 为 `"-c"`；控制台宿主加内存文件系统 |
| `static PyModuleObject? RunCode(PyEnvironment environment, string code, string? moduleName = null, string? sourceName = null)` | 在指定环境中执行，I/O、文件系统与搜索路径均由该环境决定 |
| `static void RunRepl(PyEnvironment? environment = null)` | 交互式 REPL，可传入已有环境。循环内异常打印后继续，方法不返回 |
| `static PyInterpreter Create(PyEnvironment environment)` | 基于给定环境创建解释器实例；`environment` 为 `null` 时抛 `ArgumentNullException` |

## 实例方法

| 成员 | 说明 |
| --- | --- |
| `void Execute(string code, string sourceName)` | 在 `__main__` 上编译并执行一段源码。`code` 或 `sourceName` 为 `null` 时抛 `ArgumentNullException` |
| `void Execute(PyCodeObject codeObj)` | 执行已编译的代码对象（Python 侧 `compile()` 的产物） |
| `PyModuleObject MakeModule(string moduleName)` | 把当前 `__main__` 的全部属性复制进新模块并返回，`__name__` 为传入名 |
| `void Dispose()` | 释放内部执行上下文与环境 |

## 行为备注

- 实例上的多次 `Execute` 共享同一个 `__main__` 模块，变量、函数与 import 状态跨执行保留。
- 异常传播：`RunFile`、`RunCode`、`Execute` 都会在 Python 异常未被捕获时抛出
  `PyRuntimeException`。抛出前统一经 `PyTryCatch` 处理，即先向环境错误流写出 traceback
  （宿主支持颜色时带 ANSI 红）并设置环境退出码，再抛出。`RunRepl` 在循环内打印后继续，
  不结束会话。
- `SystemExit` 由 `PyTryCatch` 从异常的 `code` 属性解析出退出码并写入环境，随后同样抛出。
  CLI 宿主按退出码决定进程状态。
- REPL 的续行逻辑：编译得到 `SyntaxError` 且词法流已到文件尾（`EndMarker`）时继续读入下一行；
  `IndentationError` 会先消费多余的 `Dedent`。

## 相关节点

- [PyEnvironment 参考](./PyEnvironment.md)、[PyEnvironmentHost 参考](./PyEnvironmentHost.md)
- [错误处理](../user-guide/error-handling.md)
