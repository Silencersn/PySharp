# 错误消息规范（PySR）

源码：`PySharp/Resources/`

Python 侧可见的一切错误消息都集中在 `PySR`。它不是 `.resx` 资源文件，而是按域拆分的 `internal static partial class`，内容为 `public const string` 常量。使用者可见的异常措辞演进与一致性都由这套常量保证。

## 文件组织

| 文件 | 域前缀 | 内容 |
| --- | --- | --- |
| `PySR.cs` | — | `PySR.Format(...)` 助手（复合格式化） |
| `PySR.Syntax.cs` | `InvalidSyntax_` | 词法与语法错误消息 |
| `PySR.Runtime.cs` | `Runtime_` | 运行时通用消息，按语句或特性二级分组 |
| `PySR.Runtime.Queue.cs` / `PySR.Runtime.Random.cs` / `PySR.Runtime.Threading.cs` | `Runtime_Queue_` / `Runtime_Random_` / `Runtime_Threading_` | 单模块消息，模块大了就拆独立文件 |
| `PySR.Unicode.cs` | `Unicode_` | 编解码相关消息 |

## 常量命名与书写规范

从现有代码归纳，新消息必须遵循：

```csharp
public const string Runtime_WithStmt_MissingEnter =
    "'{0}' object does not support the context manager protocol (missed __enter__ method)";
```

- **命名**：`<域>_<语法结构或特性>_<现象>` 三段式，PascalCase，下划线分段，例如 `Runtime_Import_ModuleNotFound`、`InvalidSyntax_Tokenize_Unterminated_StringLiteral`。
- **语言**：消息文本为英文，对齐 CPython 的用户体验；以小写开头，不加句号。
- **占位符**：使用 .NET 复合格式（`{0}`、`{1}`），不要用字符串插值或拼接。消费端统一经 `PySR.Format(format, args)`，其签名为 `params ReadOnlySpan<object?>`，无参时零格式化开销。
- **占位内容约定**：类型名用 `'{0}' object`、`{0}.__xxx__ must be ...` 的形式引用协议细节，对齐 CPython 的常见措辞，如 `unsupported operand type(s) for +: '{0}' and '{1}'`。

## 消费方式

```csharp
// 错误即值路径，绝大多数场景
return PyResult.TypeError(PySR.Runtime_Object_SpecialMethodReturnsWrongType,
    PySpecialNames.Len, "int", obj.PyType.FullName);

// 带编译器定位的语法错误（词法、语法、语义分析阶段）
throw SyntaxError(PySR.InvalidSyntax_Tokenize_Unterminated_StringLiteral, Lineno);

// 运行期抛出助手，生成于 PyCallContext.ExceptionThrows.g.cs
throw context.TypeError(PySR.Runtime_Import_PackageNotString);
```

`PyResult.<X>Error(format, args)` 工厂由异常清单驱动生成，见[源生成器](../internals/source-generators.md)，内部就是 `RaiseException(type, format, args)` 转 `PySR.Format`。

## 新增消息的流程

1. 判断归属域：语法期消息进 `PySR.Syntax.cs`；运行时通用消息进 `PySR.Runtime.cs`；某模块专属且条目多时参照 `Queue` / `Random` / `Threading` 拆 `PySR.Runtime.<Module>.cs`；
2. 按三段式命名追加常量，同结构的消息放在一起，现有文件内已按语句或特性聚类排列；
3. 消费处用 `PyResult.<X>Error(PySR.Xxx, args)`，占位实参按顺序传入；
4. 若消息属于新协议或新语句，而不是既有消息的改写，在 `test_pyfiles` 里断言该错误确实以预期类型与文本出现，参照 `test_type_error_messages.py` 的做法；
5. 提交前 grep 确认没有把消息内联进实现代码，`Resources` 之外不应出现新的裸消息字符串；dunder 名字另受 PYSPI006 约束。

## 修改既有消息

- 措辞微调（对齐 CPython、修正语法）直接改常量，全量测试即可，所有引用点自动生效；
- **改占位符个数或顺序属于接口变更**：grep 常量名找出全部 `PySR.Format` 与 `PyResult.<X>Error` 调用点，逐一核对实参；
- 不保留已无引用的常量。编译期常量没有死代码警告，需要人工清理。

## 与 CPython 措辞的对齐尺度

- 异常类型与层级必须一致，这是兼容性承诺；
- 消息文本以 CPython 为基准持续逐字对齐，对齐时连同占位实参顺序与类型名来源一并核对。已完成的范围包括运算符报文三模板、str 方法族 TypeError、容器构造参数校验、异常链属性删除报文、int 转换位数限制提示等；
- 仍允许零星出入，见[与 CPython 的差异](../python-compat/cpython-differences.md)，但新消息的默认姿势是逐字引用 CPython 文案（含 argument 序号与类型名后缀），仅在 CPython 无对应措辞时使用更精确的库内措辞；
- 每批对齐附带 `test_*_regression.py` 回归（如 `test_str_error_messages_regression.py`）锁定措辞。
