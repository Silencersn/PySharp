# 测试体系

源码：`PySharp.Tests/`。

测试策略的核心选择是断言写在 Python 里，C# 只负责驱动。这保证测试语料与真实执行路径完全一致
（词法、语法、编译、VM 全链路），也覆盖了生成的类型机器。

## 工程组织

`PySharp.Tests` 使用 `MSTest.Sdk 4.0.1`，目标 `net10.0`：

| 文件 | 职责 |
| --- | --- |
| `PyFileTests.g.cs`（生成） | 源生成器扫描 `test_pyfiles/` 根层 `.py` 自动生成的 MSTest 包装，每夹具一个 `[TestMethod]` |
| `PyFixtureRunner.cs` | 生成代码调用的共享 runner：`PyInterpreter.RunFile` 驱动单个夹具 |
| `TestPyFiles.cs` | 需要特殊 host 的手写测试（注入 argv、捕获 stdin/stderr、校验 exit code、REPL、字节级输出、临时目录布局） |
| `UtilityTests.cs` | 与 Python 语义无关的 C# 工具类测试（`ConcurrentSet`、`MemoryFileSystem` 等） |
| `StdIoTests.cs`、`ColorSupportTests.cs`、`TracebackTests.cs`、`WarningTests.cs`、`ModuleProviderTests.cs` 等 | 标准流、颜色输出、traceback 渲染、警告机制与模块提供器的 C# 侧测试 |
| `MSTestSettings.cs` | MSTest 配置（方法级并行） |
| `test_pyfiles/` | Python 断言语料，作为 `Content` 复制到输出目录，同时以 `AdditionalFiles` 喂给生成器 |

## 生成与驱动

`PySharp.Tests.SourceGeneration` 是测试项目专属的 `IIncrementalGenerator`：csproj 用
`<AdditionalFiles Include="test_pyfiles\*.py" />` 把根层夹具喂给生成器，生成器解析每个文件的
docstring 头部元数据（`:kind: test` 生成测试、`:kind: helper` 跳过），发射一个 `PyFileTests`
测试类。语料的元数据与命名规范见[测试语料规范](../contributing/test-corpus.md)。

生成的方法体只做一件事——调用共享 runner：

```csharp
[TestMethod]
public void TestFloatAbs()
{
    PyFixtureRunner.Run("test_float_abs.py");
}
```

runner 复刻原驱动语义：`PyInterpreter.RunFile(Path.Combine("test_pyfiles", fileName))`，走真实
文件系统与默认 host。夹具 print 输出仅用于辅助调试，框架不校验其内容。

- 脚本内部以 `assert` 或主动 `raise` 自校验。断言失败即 `AssertionError` 未捕获，
  转为 `PyRuntimeException`，测试失败；脚本跑完即通过。
- 生成器同时承担守门：文件名含 `regression`、缺 docstring、`:kind:` 非法等直接 build error，
  挡住命名回潮（诊断表见语料规范）。

## 主题覆盖

主题覆盖与[语言特性支持清单](../python-compat/language-features.md)一一对应：类、元类、描述符与
特殊方法，闭包与默认参数，生成器与 `yield`，异步（`async for`、异步生成器、异步推导式），
推导式，`match`、海象运算符与解包，f-string、t-string 与 `%` 格式化，全部容器类型的全集与边界，
数值解析与格式化，异常与异常组，泛型与 `TypeVar`，`import`、相对导入与 `sys.argv`，
`math`、`time`、`random`、`queue`、`open`、`compile`、`exec`、`eval`，以及排序比较等。

## 新增测试的惯例

1. 语言与语义行为：新建 `test_pyfiles/test_<行为契约>.py`，断言全部写在 Python 内，docstring 按语料
   规范写——保存即完成，生成器自动注册测试。
2. 需要特殊 host 的场景：夹具照常写，测试进 `TestPyFiles.cs` 手写。
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
