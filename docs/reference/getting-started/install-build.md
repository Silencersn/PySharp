# 安装与构建

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 支持的平台与 .NET 10 一致（Windows、Linux、macOS）

## 获取与构建

```bash
git clone <仓库地址>
cd PySharp

dotnet build PySharp.slnx   # 构建整个解决方案
dotnet test  PySharp.slnx   # 运行测试套件
```

解决方案文件为 XML 格式的 `PySharp.slnx`，包含核心库、命令行宿主、测试，以及两对源生成器与
分析器项目，见[项目概览](./overview.md)中的组成表。

只构建核心库：

```bash
dotnet build PySharp/PySharp.csproj
```

## 在你的项目中引用

当前项目以源码形式集成，在你的 `.csproj` 中添加项目引用：

```xml
<ItemGroup>
  <ProjectReference Include="path/to/PySharp/PySharp.csproj" />
</ItemGroup>
```

核心库的目标框架为 `net10.0`，引用方项目需要同目标或更高的兼容框架。库已设置 `IsTrimmable`
与 `IsAotCompatible`，可随宿主应用做裁剪与 AOT 发布。

主要命名空间：

| 命名空间 | 内容 |
| --- | --- |
| `PySharp.Runtime` | `PyInterpreter`、`PyOperators`、`PySpecialMethods`、`PyRuntimeException` 等运行时入口 |
| `PySharp.Runtime.Environments` | `PyEnvironment`、`PyEnvironmentHost` 及其构建器接口 |
| `PySharp.Runtime.IO`、`PySharp.Runtime.IO.Memory`、`PySharp.Runtime.IO.Physical` | 虚拟文件系统的抽象与实现 |
| `PySharp.Runtime.Calls`、`PySharp.Runtime.Calls.Extensions` | `PyResult` 与调用扩展方法 |
| `PySharp.Modules.Builtins` | `PyObject` 及内建 `Py*Object` 类型 |
| `PySharp.Runtime.PyAttributes` | `[PyType]`、`[PyMethod]` 等扩展特性 |

## 命令行工具

`PySharp.Console` 项目构建出行为类似 `python` 命令的可执行程序，支持 `-h`、`--help`、`-V`、
`--version`、`-c <code>`、`-O`、`-OO`、`-S`、`-`（从标准输入读取）与 `--` 选项，脚本执行时
透传 `sys.argv`，无参数启动进入交互式 REPL。逐项参考见
[命令行与交互模式](../user-guide/cli-and-repl.md)。

用 `dotnet run` 试用：

```bash
dotnet run --project PySharp.Console              # 进入 REPL
dotnet run --project PySharp.Console -- -c "print(1 + 2)"
dotnet run --project PySharp.Console -- script.py arg1 arg2
```

## 下一步

- [快速上手](./quickstart.md)：在你的代码中执行第一段 Python
