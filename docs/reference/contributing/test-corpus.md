# 测试语料规范

`test_pyfiles/` 下的 Python 测试语料由源生成器自动发现并生成 MSTest 包装，本文是夹具的命名、结构与元数据的规范正典。运行链路与工程组织的机制说明见[internals 的测试体系](../internals/testing.md)。

## 核心原则

1. **测试按行为契约命名与描述**，不以历史事故命名。标识符里禁止出现 `regression`——出身背景写在 `:background:` 字段里，属于文档，不属于名字。生成器对文件名里的 `regression` 直接报 error。
2. **断言全部写在 Python 里**，脚本能独立跑完即通过；C# 侧只负责驱动。
3. **夹具的 print 输出仅用于辅助调试**。语料自身的通过/失败判定只看脚本的退出行为（未捕获异常即失败），不依赖 print 的内容。print 的价值有二：本地调试时人眼对照；以及 CPython 输出对比层——生成器会为每个 `test` 夹具发射一个对比测试，把夹具作为脚本分别在 PySharp.Console 与本地 CPython 3.14 下运行，校验两侧的退出码与归一化后的 stdout 一致（见下文[CPython 输出对比](#cpython-输出对比)）。

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
| `:cpython-diff:` | 否 | 自由文本（单行） | 已知 CPython 分歧登记：非空时，CPython 对比测试以该理由 `[Ignore]`。理由必须描述分歧本身（"CPython 3.14 返回 mappingproxy 而 PySharp 返回 dict"），修复落地后**必须移除豁免**。空理由触发 PYFIX009 warning。字段对 `helper` 无意义 |

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
| `:cpython-diff:` 值为空 | warning (PYFIX009) |

生成范围是 `test_pyfiles/` **根层**的 `.py`（子目录天然排除，包内模块不会被扫描）。

## CPython 输出对比

每个 `test` 夹具除普通测试外，还会在生成类 `PyFileCpythonTests` 中得到一个对比测试（驱动在 `PyCpythonDiffRunner`）：夹具作为脚本分别在 PySharp.Console 与本地 CPython 3.14 的子进程中运行，两侧判定一致才通过。

**一致判定**：

- **退出码对称**：双侧 0 或双侧非 0。双侧都失败时要求最终异常类型名相同（stderr 末行的 `XxxError:`），不比对 traceback 文本——两侧的 traceback 格式本就不同。
- **stdout 归一化后一致**：三类差异被归一化吸收——CRLF/LF 与行尾空白（Windows 管道下 CPython 输出 CRLF、PySharp 写裸 LF，后者本身是待裁定项）、对象默认 repr 里的 `0x` 地址、绝对路径。其余内容即行为契约。
- **stderr 不比对**：警告与 unraisable 的格式两侧仍有已知分歧（独立跟踪），stderr 属诊断输出，不构成行为契约。

**环境与运行模型**：

- CPython 探测顺序：环境变量 `PYSHARP_CPYTHON`（指向 3.14 的 python.exe，多版本共存时用它显式指定）→ `py -3.14` → PATH 上的 `python`/`python3`，要求版本为 `Python 3.14.x`。找不到时对比测试逐个 Inconclusive，不算失败——本层是"环境具备即校验"，不是硬依赖。
- 每次运行使用独立临时工作目录：夹具的相对文件 IO 落在临时目录，不污染语料与仓库。stdin 重定向为空（`input()` 两侧对称得到 EOFError）。单次超时 60 秒，超时按分歧处理。
- 并发限流 8 路（叠加 MSTest 方法级并行）；实测整层使全量 `dotnet test` 增加约 20–40 秒（386 夹具 × 双侧子进程）。

**豁免流程（`:cpython-diff:`）**：发现分歧 → 上报 issue → 夹具 docstring 登记 `:cpython-diff: <分歧描述>`（对比测试转为 `[Ignore]`，理由即豁免登记）→ 修复落地 → 移除豁免。豁免是待清零的登记簿，不允许无理由登记（空理由触发 PYFIX009）。

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
- [ ] `dotnet test` 中能看到对应生成的测试（普通 + CPython 对比各一）
- [ ] 与 CPython 3.14 存在已知分歧时，登记 `:cpython-diff:` 并附 issue 链接
