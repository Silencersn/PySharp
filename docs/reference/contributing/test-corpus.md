# 测试语料规范

`test_pyfiles/` 下的 Python 测试语料由源生成器自动发现并生成 MSTest 包装，本文是夹具的命名、结构与元数据的规范正典。运行链路与工程组织的机制说明见[internals 的测试体系](../internals/testing.md)。

## 核心原则

1. **测试按行为契约命名与描述**，不以历史事故命名。标识符里禁止出现 `regression`——出身背景写在 `:background:` 字段里，属于文档，不属于名字。生成器对文件名里的 `regression` 直接报 error。
2. **断言全部写在 Python 里**，脚本能独立跑完即通过；C# 侧只负责驱动。
3. **夹具的 print 输出仅用于辅助调试**。框架只校验脚本的退出行为（未捕获异常即失败），不校验 stdout/stderr 的内容。结尾的 `print("<fixture> passed")` 是给人在控制台对照用的，不是断言的一部分。与 CPython 的输出对比（diff 两侧 stdout）是尚未开工的独立工作，届时会另行引入机制，不依赖现在的 print。

## 文件头元数据

每个夹具必须以模块 docstring 开头（允许 UTF-8 BOM 与前导注释行），docstring 内含结构化字段列表：

```python
"""Float abs() clears the sign bit of a float and converts exact subtypes.

abs(-0.0) is +0.0, and math.copysign sees a positive result. int.__abs__
converts exact subtypes (bool) to pooled ints.

:kind: test
:background: Originally a regression guard for float.__abs__; the sign
    semantics follow CPython float_abs / fabs.
"""
```

- **标题行**：一句行为陈述（"X does Y under Z"），不是话题标签。
- **正文**：补充断言覆盖点与语义细节，可多段。
- **字段列表**：docutils 风格的 `:name: value`，value 可续行（缩进）。

| 字段 | 必填 | 取值 | 说明 |
| --- | --- | --- | --- |
| `:kind:` | 是 | `test` / `helper` | `test` 生成 MSTest 测试；`helper` 跳过生成。除被其他夹具 import 的辅助件外，**需要特殊 host 驱动的夹具**（注入 argv、捕获 stdio 等，由 `TestPyFiles.cs` 手写测试充当驱动者）也标 `helper`，并在 docstring 注明驱动者。必填而非默认，防止漏标辅助件时静默生成假测试 |
| `:background:` | 否 | 自由文本 | 出身背景：当初的缺陷背景、CPython 行为引用（含版本与源码位置）。语义上承接原"回归"叙事 |

解析约束：支持 `"""` 与 `'''`；docstring 内不做转义处理，引号串不要出现在头部；未知字段触发 warning——新增字段属于生成器改动，先改生成器再使用。

## 命名

- 测试夹具：`test_<行为契约>.py`，全小写下划线分词，如 `test_str_splitlines_keepends_keyword.py`。
- 辅助件：名字描述它作为模块提供什么，如 `test_imported.py`、`import_cycle_moda.py`；被 import 的名字与文件名一致（`import test_imported`）。
- 多文件场景（包、循环导入家族）放 `test_pyfiles` 子目录或平铺同前缀家族，成员均标 `:kind: helper`，由一个主夹具承载断言。

## 生成器行为

| 检查 | 级别 |
| --- | --- |
| 缺 docstring / `:kind:` 缺失或非法 | error |
| 文件名含 `regression` | error |
| 方法名映射冲突 | error |
| 未知元数据字段 | warning |
| docstring 标题行以 `Regression` 开头 | warning |

生成范围是 `test_pyfiles/` **根层**的 `.py`（子目录天然排除，包内模块不会被扫描）。

## 新增测试的流程

1. 语言与语义行为：新建 `test_pyfiles/test_<行为契约>.py`，断言写在 Python 内，docstring 按上述规范写，保存即完成——生成器自动注册测试，无需改 C#。
2. 需要特殊 host 的场景（注入 argv、捕获 stdin/stderr、校验 exit code、REPL、字节级输出）：进 `TestPyFiles.cs` 手写测试，夹具本身仍按本规范写。
3. 辅助件：正常编写，docstring 标 `:kind: helper`。
4. C# 工具类测试进 `UtilityTests.cs`，不走语料。

## 收尾 checklist

- [ ] 文件名是行为契约，无 `regression`
- [ ] docstring 有标题行、正文、`:kind:`；有出身背景时写 `:background:`
- [ ] 断言自校验，失败路径有明确 AssertionError 消息
- [ ] 结尾 print 与文件名一致（仅调试用途）
- [ ] `dotnet test` 中能看到对应生成的测试
