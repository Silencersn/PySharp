# 测试体系

源码：`PySharp.Tests/`。

测试策略的核心选择是断言写在 Python 里，C# 只负责驱动。这保证测试语料与真实执行路径完全一致
（词法、语法、编译、VM 全链路），也覆盖了生成的类型机器。

## 工程组织

`PySharp.Tests` 使用 `MSTest.Sdk 4.0.1`，目标 `net10.0`：

| 文件 | 职责 |
| --- | --- |
| `TestPyFiles.cs`（4589 行） | 主体：每个 `[TestMethod]` 运行 `test_pyfiles/` 下的一个 `.py` 脚本 |
| `UtilityTests.cs` | 与 Python 语义无关的 C# 工具类测试（`ConcurrentSet`、`MemoryFileSystem` 等） |
| `StdIoTests.cs`、`ColorSupportTests.cs`、`TracebackTests.cs`、`WarningTests.cs` | 标准流、颜色输出、traceback 渲染与警告机制的 C# 侧测试 |
| `MSTestSettings.cs` | MSTest 配置 |
| `test_pyfiles/`（387 个脚本） | Python 断言语料，作为 `Content` 复制到输出目录 |

驱动模式：

```csharp
private static PyModuleObject RunModule(string filename)
    => PyInterpreter.RunFile(Path.Combine("test_pyfiles", filename));   // 物理文件系统路径

[TestMethod]
public void TestMatchCase()
{
    var module = RunModule("test_match_case.py");
    Assert.IsNotNull(module);
}
```

- 脚本内部以 `assert` 或主动 `raise` 自校验。断言失败即 `AssertionError` 未捕获，
  转为 `PyRuntimeException`，测试失败；脚本跑完即通过。
- 少量行为直接在 C# 侧断言，例如
  `Assert.ThrowsExactly<PyRuntimeException>(() => PyInterpreter.RunCode("raise TypeError"))`。

## 语料命名与覆盖

命名约定：`test_<主题>.py` 为主体验证，`test_<主题>_extended.py` 扩展边界，
`test_<现象>_regression.py` 是缺陷回归，以当初的故障现象命名，如
`test_slice_step_zero_regression.py`、`test_float_format_regression.py`。包导入测试用
`test_pkg/` 目录。

主题覆盖与[语言特性支持清单](../python-compat/language-features.md)一一对应：类、元类、描述符与
特殊方法，闭包与默认参数，生成器与 `yield`，异步（`async for`、异步生成器、异步推导式），
推导式，`match`、海象运算符与解包，f-string、t-string 与 `%` 格式化，全部容器类型的全集与边界，
数值解析与格式化，异常与异常组，泛型与 `TypeVar`，`import`、相对导入与 `sys.argv`，
`math`、`time`、`random`、`queue`、`open`、`compile`、`exec`、`eval`，以及排序比较等。

## 新增测试的惯例

1. 语言与语义行为：新建 `test_pyfiles/test_<主题>.py`，断言全部写在 Python 内，遵循「脚本能独立
   跑完即通过」的约定，再在 `TestPyFiles.cs` 加一个同名的 `[TestMethod]`。
2. 缺陷修复：命名 `test_<现象>_regression.py`，用最小复现覆盖原故障。
3. C# 工具类：进 `UtilityTests.cs`。
4. 需要多文件的场景（包、被导入模块）放 `test_pyfiles` 的子目录，csproj 已含 `**/*` 通配。

## 运行

```bash
dotnet test PySharp.slnx            # 全量
dotnet test --filter "TestMethod=TestMatchCase"
```

构建与测试的更多环境说明见[构建与测试](../contributing/build-and-test.md)。生成器改动的验证路径
（自举加全量测试）见[源生成器与代码分析](./source-generators.md)。
