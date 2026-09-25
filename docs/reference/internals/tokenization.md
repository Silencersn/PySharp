# 词法分析

源码：`PySharp/Compilation/Tokenization/`。

词法分析把源码字符流切分为带位置信息的 `Token` 序列，并完成 Python 特有的行级语法处理，
包括缩进栈（`Indent` 与 `Dedent`）、换行语义、显式行拼接，以及 f-string 与 t-string 的嵌套替换域。

## 文件一览

| 文件 | 职责 |
| --- | --- |
| `Lexer.cs`（1059 行） | 核心状态机，入口 `Lexer.Tokenize(context, codeSource, extraNewLine)` |
| `Token.cs` | `readonly record struct Token`，含 `TokenType` 与 `CodeTextSpan`，附带行定位辅助 |
| `TokenType.cs` | 全部词法单元种类，含下划线前缀的内部状态型 token |
| `TokenSequence.cs` | token 列表的轻量包装，支持按索引与 `AsSpan()` 访问 |
| `LexerRegexes.cs`、`LexerRegexPatterns.cs` | 匹配用的正则模式，部分预编译 |
| `UnicodeIdentifier.cs` | 标识符校验：按 XID_Start 与 XID_Continue 判定 |

## Lexer 状态机

```csharp
private enum LexerState : byte
{
    Default,                                     // 常规代码
    TokenizingMultiLineSingleOrDoubleString,     // 反斜杠续行的普通字符串
    TokenizingTripleString,                      // 三引号字符串
    FStringMiddle,                               // f-string 与 t-string 的字面文本段
    FStringDefault,                              // 替换域内的表达式，按 Default 规则切词
}
```

关键机制：

- 缩进栈 `_indentationLevels`：行首空白与栈顶比较，产生 `Indent` 与 `Dedent` token。
  `InternalClearIndentation` 在文件尾把栈清到 0，每个层级补一个 `Dedent`，最后统一追加
  `EndMarker`。
- 显式行拼接：反斜杠续行与括号内换行（`_parenLevel`）抑制 `NewLine` 的产生，改以 `NL` 代替。
  parser 侧会跳过 `NL` 与 `Comment`。
- f-string 与 t-string 嵌套：`FStringInfo` 记录包裹字符、是否三引号、是否模板串、进入时的括号
  层级与格式说明符层级，`_fstringStack` 支持替换域内再嵌套字符串。状态在 `FStringMiddle`
  （扫描 `{`、`}` 与包裹符，并处理转义跳过）与 `FStringDefault`（按普通代码切词，按括号层级判定
  替换域结束或进入格式说明符）之间切换。
- token 匹配：`TokenizeToken` 用正则组匹配当前偏移。`Operator` 类 token 先统一识别，再经
  `Token.GetExactTokenType` 按文本精确分类。
- 起始与收尾：`InternalStart` 发出零长的 `Encoding` token；`InternalEnd` 保证末尾有 `NewLine`、
  清空缩进并发 `EndMarker`；未闭合字符串按状态报不同的 `SyntaxError`。

## Token 与错误报告

- `Token` 是紧凑布局的 struct，只存 `TokenType` 与 `CodeTextSpan`（偏移区间）。文本经
  `CodeSource.Code.GetString(span)` 按需取出，避免为每个 token 分配字符串。
- `Lexer` 实现 `ICodeMetaInfoProvider`。抛出语法错误时携带当前行号，形成带源码定位的
  `PySyntaxErrorObjectType` 异常。
- REPL 场景下 `extraNewLine: true` 会在 `EndMarker` 前插入一个 `NewLine`，使「空行结束块」的
  交互语义可用。

## 源码字节解码

词法分析消费的是字符流。字节到字符的解码由 `Compilation/PySourceDecoder.cs` 完成，镜像 CPython
tokenizer 的解码管线，统一接入源码加载、`import`、`compile`、`exec` 与 `eval`：

1. 检测并剥离 UTF-8 BOM。
2. 解析编码声明（coding cookie）：只扫前两行，且仅当之前所有行都是空白到 `#` 的注释行才生效，
   对应 CPython 的 `check_coding_spec` 语义；声明与 BOM 并存且非 `utf-8` 时报 `SyntaxError`。
3. 编码为 `utf-8` 或未声明时，按 CPython 的 `valid_utf8` 规则逐序列校验，拒绝游离前导字节、
   过长编码、代理区与越界平面，坏字节报 `SyntaxError` 并带行号与列偏移。
4. 其他编解码器经 .NET `Encoding.GetEncoding` 解析，代码页已注册，`utf-16-le` 与 `utf-16-be`
   的虚线拼写特判，`_` 归一化为 `-`，`latin-1` 族归一化。解码采用严格回退，任何坏字节抛
   `SyntaxError`，不让 .NET 默认的替换字符静默混入。
5. 编码名不可识别时报 `SyntaxError: unknown encoding`。

声明解析支持 `# -*- coding: <name> -*-` 与 `# coding=<name>` 两种形态。

## 与相邻层的接口

输入方向，源码字节先经 `PySourceDecoder.Decode` 得到字符流，再包成 `CodeSource`（文件名加
`CodeText` 行索引），见 `Compilation/CodeAnalysis/`。输出方向，`TokenSequence` 交给
`Parser.ParseModule`、`ParseExpression` 或 `ParseInteractive`，见[语法分析](./parsing.md)。
