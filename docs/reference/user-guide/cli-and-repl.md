# 命令行与交互模式

源码：`PySharp.Console/Program.cs`（CLI 驱动）、`PySharp/Runtime/PyInterpreter.cs`（`RunRepl`）、
`PySharp/Runtime/PyCore.cs`（`DisplayHook`）。

本篇是 `pysharp` 命令行工具与交互式解释器的参考。嵌入 API 不经命令行，可按需跳到
[嵌入侧的 REPL](#嵌入侧的-repl)。

## 命令行

```text
Usage: pysharp [options] [script] [arg ...]
```

| 选项 | 行为 |
| --- | --- |
| `-h`、`--help` | 打印用法说明后退出，退出码 0 |
| `-V`、`--version` | 打印 PySharp 版本后退出 |
| `-c <code>` | 把 `<code>` 作为 Python 程序执行；`sys.argv[0]` 为 `'-c'`，其后参数透传 |
| `-` | 从 stdin 读入程序；stdin 连接终端时进入交互 REPL |
| `--` | 终止选项解析，其后第一个参数视为脚本名（即使以 `-` 开头）；`pysharp --` 后无脚本则进 REPL；`--` 后的 `-` 仍选 stdin 模式 |
| `-O` | 优化级加一：移除 `assert` 与依赖 `__debug__` 的语句 |
| `-OO` | 优化级加二：在 `-O` 基础上再丢弃 docstring |
| `-S` | 初始化时不隐式执行 `import site` |
| `script arg ...` | 执行脚本文件；`sys.argv` 为 `['script.py', 'arg1', ...]` |

选项解析细节：

- `-O`、`-OO`、`-S` 是全局开关，在参数序列最前生效、可组合（`-OO` 也可由两个 `-O` 累计而来），
  优化级写入环境的 `SetOptimizationLevel`，编译期生效，见[配置执行环境](./environment.md)。
- 首个非全局开关的参数结束选项区；以 `-` 开头但不在上表的参数报 `unknown option`，退出码 2。
- 脚本路径是目录时报 `<path> is a directory, cannot continue`，退出码 1。

退出码约定：

| 结束方式 | 退出码 |
| --- | --- |
| 正常结束、`sys.exit()` 无参、`sys.exit` 传非整数 | 0 |
| `sys.exit(n)` 传整数 `n` | `n`；超出范围的巨整数视为 1 |
| 未捕获 Python 异常（traceback 打印到 stderr） | 1 |
| 命令行用法错误（未知选项、`-c` 缺参、脚本不存在） | 2 |

尚未支持 CPython 的 `-m`（按模块名运行）与 `-i`（执行后进入交互）。`-m` 的替代做法是把入口脚本
放进内存文件系统或物理目录后运行，见[虚拟文件系统](./virtual-file-system.md)。

## 交互模式

无参数（或仅全局开关、或 `pysharp --`、或 `-` 且 stdin 是终端）启动时进入 REPL：

- 横幅：`PySharp (v<版本>) on <操作系统>`。
- 提示符：`>>> `，续行为 `... `。
- 多行续行判定：输入经 single 模式编译，若得到 `SyntaxError` 且解析器尚未到达输入末尾（括号未
  闭合、`def` 体未完等），则静默续行；输入已完整却仍有语法错误时立即打印错误并回到 `>>>`。
  缩进块以空行结束，REPL 会在输入尾部补一个换行以触发 `Dedent`。
- 表达式回显与 `_`：交互模式下顶层表达式语句的值经 `sys.displayhook` 机制回显，非 `None` 时打印其
  `repr()`，并把值绑定到 `builtins._`，因此上一条结果可用 `_` 取回。
- `sys.exit()`：在 REPL 中被解释器捕获，退出码记入环境，会话继续；以 EOF 结束会话。
- 错误呈现：循环捕获每个顶层异常并打印后继续；traceback 是否带 ANSI 颜色由宿主决定，见
  [宿主与 I/O 重定向](./hosting.md)。

REPL 的 `sys.path[0]` 预置为当前工作目录，本地模块可直接 `import`。

## 嵌入侧的 REPL

REPL 是普通库能力，不绑定控制台程序：

```csharp
PyInterpreter.RunRepl();                    // 默认：CreateRepl 宿主加内存 FS
PyInterpreter.RunRepl(environment);         // 或传入自建环境，可换物理 FS、重定向 I/O
```

- 默认环境由 `PyEnvironmentHost.CreateRepl()` 构建，并以 `SetInteractive(true)` 打开交互标记。
- 自定义宿主需要提供可读的 `In`（`ReadLine()` 返回 `null` 即视为 EOF，结束会话）与可写的 `Out`
  （提示符、横幅与回显都写到这里）。
- 文件系统默认是内存 FS。要让 REPL 用户 `import` 本地模块，需显式换物理 FS 或 `AddPath`；
  控制台宿主 `CreateConsole(usingPhysicalFileSystem: true)` 已预置物理 FS，见
  [宿主与 I/O 重定向](./hosting.md)。

## 相关节点

- [安装与构建](../getting-started/install-build.md)：`pysharp` 可执行文件与 `dotnet run` 试用方式
- [与 CPython 的差异](../python-compat/cpython-differences.md)：工具链差异的汇总视角
- [执行 Python 代码](./executing-python.md)：`Execute`、`RunCode`、`RunFile` 的嵌入 API
