# 测试体系

源码：`PySharp.Tests/`。

测试策略的核心选择是断言写在 Python 里，C# 只负责驱动。这保证测试语料与真实执行路径完全一致
（词法、语法、编译、VM 全链路），也覆盖了生成的类型机器。

## 工程组织

`PySharp.Tests` 使用 `MSTest.Sdk 4.0.1`，目标 `net10.0`：

| 文件 | 职责 |
| --- | --- |
| `PyFileTests.g.cs`、`PyFileCpythonTests.g.cs`（生成） | 源生成器扫描 `test_pyfiles/` 根层 `.py` 自动生成的两个 MSTest 包装：前者每夹具一个普通测试，后者每夹具一个 CPython 输出对比测试（登记了 `:cpython-diff:` 分歧的夹具带 `[Ignore]`） |
| `PyFixtureRunner.cs` | 普通测试的共享 runner：`PyInterpreter.RunFile` 驱动单个夹具 |
| `PyCpythonDiffRunner.cs` | 对比测试的共享 runner：把夹具作为脚本在 PySharp.Console 与本地 CPython 3.14 子进程中各跑一次，校验退出码与归一化 stdout 一致；同时提供 PySharp.Console 定位器（`TestPyFiles.cs` 的子进程测试也用它） |
| `TestPyFiles.cs` | 需要特殊 host 的手写测试（注入 argv、捕获 stdin/stderr、校验 exit code、REPL、字节级输出、临时目录布局） |
| `UtilityTests.cs` | 与 Python 语义无关的 C# 工具类测试（`ConcurrentSet`、`MemoryFileSystem` 等） |
| `StdIoTests.cs`、`ColorSupportTests.cs`、`TracebackTests.cs`、`WarningTests.cs`、`ModuleProviderTests.cs` 等 | 标准流、颜色输出、traceback 渲染、警告机制与模块提供器的 C# 侧测试 |
| `MSTestSettings.cs` | MSTest 配置（方法级并行） |
| `test_pyfiles/` | Python 断言语料，作为 `Content` 复制到输出目录，同时以 `AdditionalFiles` 喂给生成器 |

## 生成与驱动

`PySharp.Tests.SourceGeneration` 是测试项目专属的 `IIncrementalGenerator`：csproj 用
`<AdditionalFiles Include="test_pyfiles\*.py" />` 把根层夹具喂给生成器，生成器解析每个文件的
docstring 头部元数据（`:kind: test` 生成测试、`:kind: helper` 跳过），发射 `PyFileTests` 与
`PyFileCpythonTests` 两个测试类。语料的元数据与命名规范见[测试语料规范](../contributing/test-corpus.md)。

生成的方法体只做一件事——调用共享 runner：

```csharp
[TestMethod]
public void TestFloatAbs()
{
    PyFixtureRunner.Run("test_float_abs.py");
}
```

runner 复刻原驱动语义：`PyInterpreter.RunFile(path, args: null, host: PyEnvironmentHost.CreateFixtureRunner())`，
走真实文件系统。该宿主与 console 宿主相同，唯把 **stdin 换成 `Stream.Null`（恒为 EOF）**：夹具读
`sys.stdin` / 调 `input()` 时立即得到 EOF，而不是去读测试宿主自己的标准输入——否则在终端或
CI（stdin 为打开的管道）下 `readline()` 会无限期阻塞，且不产生任何错误输出。stdout/stderr 仍接
控制台，夹具 print 输出仅用于辅助调试，框架不校验其内容。生成的测试带 `[Timeout]`，夹具若阻塞
不返回会被报告为失败而非挂住整个运行。

- 脚本内部以 `assert` 或主动 `raise` 自校验。断言失败即 `AssertionError` 未捕获，
  转为 `PyRuntimeException`，测试失败；脚本跑完即通过。
- 生成器同时承担守门：文件名含 `regression`、缺 docstring、`:kind:` 非法等直接 build error，
  挡住命名回潮（诊断表见语料规范）。

## CPython 输出对比层

`PyFileCpythonTests` 的每个测试走 `PyCpythonDiffRunner.Run`：以两侧对等的命令行形态
（`exe <夹具绝对路径>`）在**独立临时工作目录**里分别启动 PySharp.Console 与 CPython 3.14
子进程，校验退出码对称、双侧成功时归一化 stdout 一致、双侧失败时最终异常类型一致。
子进程形态（而非进程内 `RunFile`）是有意为之——退出码、argv 注入、Windows 换行翻译这类
行为只有作为真实用户运行时才可见。

CPython 探测顺序为 `PYSHARP_CPYTHON` 环境变量 → `py -3.14` → PATH 上的 `python`/`python3`，
要求 3.14.x；找不到时对比测试逐个 Inconclusive（不失败）。归一化规则、stderr 不比对的裁定与
豁免流程（`:cpython-diff:`）见语料规范的[CPython 输出对比](../contributing/test-corpus.md#cpython-输出对比)。

## 主题覆盖

主题覆盖与[语言特性支持清单](../python-compat/language-features.md)一一对应：类、元类、描述符与
特殊方法，闭包与默认参数，生成器与 `yield`，异步（`async for`、异步生成器、异步推导式），
推导式，`match`、海象运算符与解包，f-string、t-string 与 `%` 格式化，全部容器类型的全集与边界，
数值解析与格式化，异常与异常组，泛型与 `TypeVar`，`import`、相对导入与 `sys.argv`，
`math`、`time`、`random`、`queue`、`open`、`compile`、`exec`、`eval`，以及排序比较等。

## 新增测试的惯例

1. 语言与语义行为：新建 `test_pyfiles/test_<行为契约>.py`，断言全部写在 Python 内，docstring 按语料
   规范写——保存即完成，生成器自动注册测试。
2. 需要特殊 host 的场景（注入 argv、捕获 stdio 内容、非默认 stdio 语义）：夹具照常写，测试进
   `TestPyFiles.cs` 手写；仅读 stdin 不需如此，进程内 runner 的 stdin 已是 EOF。
3. 辅助件（被 import 而非独立运行）：`:kind: helper` 标记，位置随意。
4. C# 工具类：进 `UtilityTests.cs`。
5. 多文件场景（包、被导入模块）放 `test_pyfiles` 的子目录或同前缀家族，csproj 已含 `**/*` 通配。

## 运行

```bash
dotnet test PySharp.slnx            # 全量
dotnet test --filter "TestMethod=TestFloatAbs"
```

构建与测试的更多环境说明见[构建与测试](../contributing/build-and-test.md)。生成器改动的验证路径
（自举加全量测试）见[源生成器与代码分析](./source-generators.md)。
