# 字符串系统

源码：`PySharp/Modules/Builtins/PyStrObject*.cs`、`PyStrIteratorObject.cs`。

`str` 是解释器中出场率最高的类型（属性名、常量池、标识符、traceback 全是字符串），因此它的实现
围绕一个目标组织：让常见的字符串尽量不分配、不重复。本篇覆盖值结构、驻留体系、字面量转换器与
方法注册机制。

## 文件布局

| 文件 | 行数 | 职责 |
| --- | --- | --- |
| `PyStrObject.cs` | 3167 | 值类型（构造、工厂、char 池、`PyLength` 缓存、码点视图）加 `PyStrObjectType`（44 个 `[PyMethod]`、`__format__` 覆写与全部协议覆写） |
| `PyStrObject.Intern.cs` | 106 | `InternPool`：驻留查找 |
| `PyStrObject.Converter.cs` | 619 | `PyStrConverter`：字面量转义的双向转换 |
| `PyStrObject.Py.cs` | 30 | 复杂方法的实例形态，目前是 `PyJoin` |
| `PyStrIteratorObject.cs` | 42 | str 迭代器，按码点 |

## 值类型

```csharp
public partial class PyStrObject : PyObject
{
    private const int CharPoolSize = 256;
    private static readonly PyStrObject[] _charPool;    // 静态构造期填满单字符 str

    public string Value { get; }
    public int PyLength { get; }                        // 惰性缓存：码点计数，-1 表示未算

    private PyStrObject(string value) { ... }

    public static PyStrObject FromString(string value);
    public static PyStrObject FromStringNoCache(string value);
    public static PyStrObject Empty { get; }
}
```

- 构造函数为 `private`，所有入口走工厂。`FromString` 对空串返回 `Empty`，对长度为 1 且码元小于
  256 的字符串命中 char 池，其余才真正分配。`FromStringNoCache` 跳过缓存。
- `PyLength` 是惰性字段缓存（C# 13 的 `field` 关键字）。首次访问时用 `CountCodePoints` 按码点
  计数。Python 的 `len(str)` 是码点数而非 UTF-16 码元数，这是与 .NET `string.Length` 的关键
  差异，也是下标与切片必须走码点枚举的原因。

## 码点视图

CPython 的 `str` 存储码点（PEP 393），其中 U+D800 到 U+DFFF 是普通取值。.NET 的 `System.Rune`
无法表示代理码点，`String.EnumerateRunes()` 对未配对代理会产出 U+FFFD，因此字符视图由
`PyStrObject` 内的 `ref struct CodePointEnumerator` 自行跨 UTF-16 码元步进：

```csharp
internal ref struct CodePointEnumerator(ReadOnlySpan<char> value)
{
    // 高代理后紧跟低代理时合并为 char.ConvertToUtf32，否则单码元即为一个码点
}
```

由此派生的 `CountCodePoints`、`CodePointAt` 与 `EnumerateCodePoints` 是长度、下标、切片、迭代与
`in` 的共同基础。

### 孤立代理项的权威码点序列

UTF-16 无法区分「两条孤立代理项相邻」与「一个星文字符」：`"\ud800" + "\udc00"` 的载荷与
`"\U00010000"` 逐码元相同，而 CPython 里前者是两个码点。因此 `PyStrObject` 携带一个可选的
权威码点数组（`_codePoints`）：

- 仅当产生方知道码点序列（拼接、`join`、`%` 格式化、f-string 的 `BuildString`、字面量转义、
  `*` 重复）且序列中出现相邻的孤立高+低代理项时才存在；其余字符串为 `null`，一切走原有
  UTF-16 快路径，零额外开销。不变式：逐码点经 `AppendCodePoint` 展开恰好等于 `Value`。
- 实例读路径（`PyLength`、`PyCharAt`、`EnumerateCodePoints`、下标/切片、迭代器）优先权威序列；
  `Eq`/`Hash`/比较/`in` 按码点序列进行（`hi+lo != "\U00010000"`，两者哈希不同，可各自作 dict 键）；
  `str.encode` 的共享核 `EncodeCore` 按权威序列对代理码点走 codec 错误事件（utf-8 报
  `surrogates not allowed`，`errors=` 处理器照常生效）。
- 逐段拼接用 `PyStrConcatBuilder`：段的边界一旦把孤立高代理项带到孤立低代理项前（或任一段
  自身携带权威序列），它才开始收集权威序列，之前的积累按 UTF-16 解释回溯成序列。`+` 的
  两操作数情形由 `ConcatValues` 用 O(1) 的边界码元检测走快路径或序列路径。
- 字面量路径在 `PyStrConverter` 的写入流上按「意图」跟踪：`\U` 星文转义一次写入的两个码元是
  有意代理对，转义产出的孤立代理相邻则经 `TryFromLiteralToString`/`TryFromTextToString` 的
  重载带出权威序列，Parser 构造常量与隐式拼接（`ConcatConstants`）经 `FromCodePoints` 落地。
- Python 层与 C# 层的边界：py 层 `hash`/`==` 走码点序列（`PyStrHash`/`PyStrEquals`），C# 层
  `GetHashCode()` 保持载荷哈希（引用相等语义）；`PyDictObject` 的 C# string 快捷查找只匹配
  无权威序列的键，携带序列的键对 string API 不可见——.NET `string` 本身无法表达歧义键，
  宿主应以 `PyStrObject` 构造与查询。

decode 方向（codec 解码产物重组相邻孤立代理）尚不携带权威序列，是已知边界。

## 驻留体系

`PyStrObject.InternPool` 同时提供静态入口与每环境实例（`PyEnvironment.InternPool`）：

```text
查找顺序（TryGetInternedString 与静态 FromString）：
 1. 空串 → Empty；单字符且码元小于 256 → char 池
 2. 静态冻结字典（PySpecialNames 的全部特殊名，进程级、无锁）
 3. 环境实例字典（ConcurrentDictionary，仅实例方法会到达）
 4. 都未命中 → 新建（Intern 会登记进环境字典）
```

要点：

- 静态层在类型构造期把 `PySpecialNames.EnumerateNonGeneratedNames()` 与
  `EnumerateGeneratedNames()`（即全部 dunder 名，由内部生成器枚举）做成
  `FrozenDictionary<string, PyStrObject>`，并提供
  `TryGetStaticInternedString(ReadOnlySpan<char>, out string)`。Parser 用它做标识符的 `string`
  去重，拿到已驻留的 .NET 字符串实例，避免每次解析分配。
- 环境层由运行时写入：`PyOperators.GetAttr(context, obj, "name")` 的 string 重载先经
  `context.PyEnvironment.InternPool.Intern(name)`，属性名跨调用命中同一个 `PyStrObject`，
  字典查找与哈希都能受益。
- 三个实例方法语义不同：`Intern` 无则登记，`TryGetInternedString` 只查不登记，
  `GetInternedOrNew` 查不到时返回新对象但不登记。
- 与分析器的联动：`PYSP003` 要求库内代码用 `PySpecialNames.Interned.X` 常量字段直取，而非
  `InternPool.FromString(PySpecialNames.X)`，前者绕过全部查找。

## 字面量转换器

词法层产出的字符串 token 是源码原文（含转义与前缀语义），`PyStrConverter` 负责与运行时值的双向
转换。它是 `internal static`，泛型于 `char` 与 `byte`，同时服务 `str` 与 `bytes`：

| 方法 | 方向 | 用途 |
| --- | --- | --- |
| `TryFromLiteralToString`、`TryFromLiteralToBytes` | 字面量到值 | 处理 `\n`、`\x41`、`\u`、`\U`、八进制等转义；bytes 路径拒绝非 ASCII |
| `TryFromTextToString` | 原文到值 | f-string 与 t-string 的字面文本段 |
| `FromStringToLiteral` | 值到字面量 | `repr` 输出，加引号并转义不可打印字符 |
| `FromSourceToLiteral(str, isRaw, builder)` | 源码到字面量 | 编译器侧重构 |

错误以 `ConvertError` 枚举加位置信息（`ConvertErrorInfo`）结构化返回，例如 `EndsWithEscape`、
`InvalidEscapeSequence`、`SurrogatesNotAllowed`，供词法层转成带定位的 `SyntaxError`。值类型上的
`FromLiteral` 与 `FromLiteralContent` 是面向编译期的薄包装，转换失败直接抛
`ArgumentException`，只应发生在库内测试构造时。

## 方法与协议的注册

44 个 `[PyMethod]` 全部声明在 `PyStrObject.cs` 的 `PyStrObjectType` 分部里，由 `PyTypeGenerator`
生成 `RegisterMethods`，见[源生成器](./source-generators.md)。两种实现形态：

- 薄静态包装（绝大多数）：`[PyMethod]` 加 `[PyFunctionParameters]` 的静态方法直接实现，
  例如 `Upper` 调用 `ToUpperInvariant()` 后经 `FromString` 自动回池。
- 实例实现加静态包装：逻辑复杂或需要被其他路径复用的方法放在值类型分部（`.Py.cs` 的
  `PyJoin`），`[PyMethod]` 静态方法一行转发。新增复杂方法时沿用该模式。

协议覆写集中在类型分部尾段：`Repr`、`Str`、`Hash`、`Bool`、`Len`、`Iter`、`GetItem`（切片与
码点下标）、`Add`、`Eq`、`Lt`、`Le`、`Gt`、`Ge`、`Mul`、`RMul`、`Mod`（`%` 格式化）、
`Contains`、`New`（`str(x)` 构造路径）。分发语义见[协议分发](./protocol-dispatch.md)。

## 迭代器

`PyStrIteratorObject` 按码点步进：`Iter` 返回自身，`Next` 逐码点前进并以
`PyStrObject.FromCodePoint` 产出，单字符命中池，无分配。`__reversed__`、`in`（`Contains` 槽）
与切片共用同一套码点语义。

## CPython 对齐注意点

改动前需要注意的几点：

- 长度与下标按码点。任何基于 `Value` 的直接索引都是错的，必须经码点枚举，`PyLength` 的实现是
  参照。
- `strip` 的空字符集差异：.NET 的 `Trim()` 对空 `char[]` 的语义是「去掉全部空白」，CPython 对
  `''` 是「什么都不去」，实现里有显式分支。同类边界在 `startswith` 的负数 `end`、`replace` 的
  空 `old` 等回归脚本中都有覆盖。
- 大小写映射与谓词按 Unicode 派生属性实现，不随文化变化。
- 搜索方法的 `start` 与 `end` 归化：`find`、`index`、`count`、`rfind`、`rindex`、`split` 的
  区间参数经 `__index__` 协议归化，非整数报 `TypeError`，巨负值报带消息的 `OverflowError`。
  `startswith` 与 `endswith` 支持元组参数，空前后缀按 CPython 的窗口语义匹配。
- `center`、`ljust`、`rjust`、`zfill` 的填充按 CPython 边界处理，`isprintable` 等可打印性判定
  与 CPython 一致。
- 改动 `str` 语义必须跑 `test_str_*`、`test_string_*` 与 `test_format_spec_str_regression` 全族
  回归，见[测试体系](./testing.md)。

## 相关阅读

[对象模型](./object-model.md) · [协议分发](./protocol-dispatch.md) ·
[词法分析](./tokenization.md)（f-string 状态机）·
[f-string 与格式化](./fstring-and-format.md)（`BuildString` 指令与格式说明符）·
[PyResult 参考](../api/PyResult.md)
