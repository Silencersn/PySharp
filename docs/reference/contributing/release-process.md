# 发布流程 checklist

相关配置：`Directory.Build.props`（版本号）、`PySharp/PySharp.csproj`（打包结构）。

当前发布物只有一个 NuGet 包：主包 `PySharp`（解释器库加内嵌公开源生成器与分析器，`PYSP*` 分析器随包分发）。本文给出从“代码就绪”到“发布后验证”的完整序列。

## 打包结构

主包 `PySharp.csproj` 的关键配置：

```xml
<!-- 版本号统一在 Directory.Build.props，所有工程继承 -->

<!-- 4 个工具项目以 Analyzer 方式接入构建（不拷引用程序集） -->
<ProjectReference Include="..\PySharp.SourceGeneration\..." OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<!-- 其中两个公开工具以【预构建 dll 路径】打进包的 analyzers 目录 -->
<None Include="..\PySharp.SourceGeneration\bin\$(Configuration)\netstandard2.0\PySharp.SourceGeneration.dll"
      Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
<None Include="..\PySharp.Analyzer\bin\$(Configuration)\netstandard2.0\PySharp.Analyzer.dll"
      Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
```

含义：

- 安装主包即自动获得公开生成器（`[PyType]` 一族可用）与 `PYSP*` 分析器，无需额外配置；
- 两个 `Internal` 工具项目不打进包，它们用于自举，引用了库内 internal 类型；
- `PySharp.Analyzer` 仅以裸 dll 随主包分发，其 csproj 中 `IsPackable=false` 关闭独立打包；
- **顺序敏感**：`None Include` 指向 `bin\$(Configuration)\netstandard2.0\` 的构建产物路径，pack 之前必须先以同一配置完整构建解决方案，否则包里是旧 dll 或缺文件。

## 发布序列

1. **全量验证**：`dotnet test PySharp.slnx` 全部绿灯；生成器改动另见[构建与测试](./build-and-test.md)的自举验证。
2. **版本号**：更新 `Directory.Build.props` 的 `<Version>`（唯一入口，所有工程统一继承，含 analyzers 目录两个裸 dll 的程序集版本），按惯例单独一笔提交（`chore: 更新项目版本号至 x.yy`）。
3. **Release 构建整个解决方案**，为 analyzers 目录产出最新 dll：

   ```bash
   dotnet build PySharp.slnx -c Release
   ```

4. **打包主包**：

   ```bash
   dotnet pack PySharp/PySharp.csproj -c Release
   ```

   产物 `PySharp.<version>.nupkg` 含 `lib/net10.0/PySharp.dll`（trim 与 AOT 标记随程序集携带）与 `analyzers/dotnet/cs/`（两个公开工具 dll）。
5. **发布**：

   ```bash
   dotnet nuget push PySharp.<version>.nupkg --source <源> --api-key <key>
   ```

> 尚未配置的发布元数据（按需补齐）：csproj 中没有 `RepositoryUrl` / `PackageProjectUrl` / `License` / SourceLink / 符号包（snupkg）设置。补 SourceLink 后才能在消费者端获得可调试的堆栈。

## 发布后验证

在全新的控制台项目里：

```bash
dotnet new console -o PySharpSmoke
cd PySharpSmoke
dotnet add package PySharp -v <发布的版本>
```

最小验收点：

- [ ] `PyInterpreter.RunCode("print(40 + 2)")` 输出 42，库本体可用；
- [ ] 写一个带 `[PyType]` 与 `[PyMethod]` 的最小类型，构建后能在 `obj/generated/` 或 IDE 的分析器输出里看到生成代码生效，验证 `analyzers/dotnet/cs` 内嵌工具随包安装；
- [ ] 故意写 `PyResult.FromValue(x)` 形式的 return，确认 PYSP001 警告出现，分析器在消费端激活；
- [ ] `dotnet publish -c Release` 成功，验证 trim 兼容；AOT 场景可加 `/p:PublishAot=true`；
- [ ] `dotnet list package` 确认版本号正确、无多余依赖，主包应为零 `PackageReference` 依赖。

## 相关阅读

[构建与测试](./build-and-test.md)、[公共 API 稳定性政策](./api-stability.md)、[源生成器与代码分析](../internals/source-generators.md)。
