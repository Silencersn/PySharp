# 构建与测试

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 解决方案文件为新 XML 格式 `PySharp.slnx`，需要较新的 `dotnet` 与 IDE 支持

## 常用命令

```bash
dotnet build PySharp.slnx        # 构建全部 9 个项目
dotnet test  PySharp.slnx        # 全量测试

dotnet build PySharp/PySharp.csproj                  # 只构建核心库
dotnet test --filter "TestMethod=TestMatchCase"      # 运行单个测试
```

试用命令行宿主：

```bash
dotnet run --project PySharp.Console                # REPL
dotnet run --project PySharp.Console -- -c "print(40 + 2)"
```

## 各项目构建要点

| 项目 | 说明 |
| --- | --- |
| `PySharp` | 核心库（net10.0，`IsTrimmable` / `IsAotCompatible`）。构建时同时运行 8 个源生成器（见[源生成器](../internals/source-generators.md)），`obj/.../generated/` 下产出 270 余个 `.g.cs`。生成器或清单（`PyTypeObject.Declarations.cs`、`PyExceptionObject.Types.cs`）有问题会直接编译失败 |
| `PySharp.Console` | 引用了内部分析器，`PYSPI*` 规则对其生效，可当最小嵌入方参照 |
| `PySharp.Tests` | `test_pyfiles/` 以 `Content` 复制到输出目录，测试按相对路径读取；覆盖率配置在 `CodeCoverage.runsettings` |
| `PySharp.SourceGeneration(.Internal)` / `PySharp.Analyzer(.Internal)` | netstandard2.0 的 Roslyn 工具项目；改完必须重新构建主库验证自举。Roslyn 版本收敛在 `Directory.Build.props` |
| `PySharp.Roslyn.Shared` | 生成器共用的编译期工具库（netstandard2.0，`IsPackable=false`）；改动它同样要重新构建主库验证自举 |

## 验证你的改动

具体任务（新增类型、模块、异常、协议槽）的分步清单见[操作指南](./adding-types-and-modules.md)；故障定位方法见[调试指南](./debugging.md)。

1. **改运行时、对象或标准库**：全量 `dotnet test`，针对性主题跑单测；
2. **改编译前端（词法、语法、语义、发射）**：全量测试（语料覆盖广）加手工 REPL 冒烟（多行块、语法错误定位）；
3. **改生成器或分析器**：先 `dotnet build PySharp/PySharp.csproj` 看自举是否通过（生成产物合法性与 PYARG 诊断），再全量测试；
4. **新增 Python 语义行为**：按[测试体系](../internals/testing.md)的惯例补 `test_pyfiles/test_<主题>.py`（断言写在 Python 内）并在 `TestPyFiles.cs` 登记；缺陷修复用 `test_<现象>_regression.py` 命名。

## 版本与发布

- 版本号维护在 `PySharp/PySharp.csproj` 的 `Version`，当前为 0.50；发版惯例是单独一笔版本提交（`chore: 更新项目版本号至 x.yy`）。
- 主 NuGet 包会内嵌公开生成器与分析器（`analyzers/dotnet/cs`）；`PySharp.Analyzer` 可独立打包。完整的打包顺序、推送与发布后验证见[发布流程 checklist](./release-process.md)。

## 提交信息惯例

提交信息用中文。重构与修复类提交使用 conventional 类型前缀加半角冒号，例如 `refactor: 统一 list 与 tuple 的下标及切片访问逻辑`、`fix: 字节串 repr 使用实际类型名`；版本号提交用 `chore:`（`chore: 更新项目版本号至 0.50`）。一行说清动机与范围即可。

## 代码风格

构建即强制，分析器 Warning 一律处理，不留存量，规则见[编码规范](./coding-standards.md)。
