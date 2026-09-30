# 编码规范

代码风格由内建 Roslyn 分析器在每次构建时强制，规则全部为 Warning 级，项目要求清零存量。规范分三层：所有使用者可见的 `PYSP*`（随 `PySharp.Analyzer` 包分发）；仅约束库自身的 `PYSPI*`（`PySharp.Analyzer.Internal`，`PySharp` 领域约定）；以及 `PYSPS*`（`PySharp.Analyzer.Style`，与 PySharp 领域解耦的通用 C# 风格，可整体抽出复用）。`PYSPI*` 只对 `PySharp` 与 `PySharp.Console` 生效；`PYSPS*` 是通用风格，解决方案内全部项目（含测试与生成器项目）都已接线，改动任一项目都受其约束。规则机理见[源生成器与代码分析](../internals/source-generators.md)。

## 命名（PYSPI002）

- `PyObject` 子类必须命名为 `Py<Name>Object`，`PyTypeObject` 子类必须命名为 `Py<Name>ObjectType`；
- 少量历史豁免内置在白名单里：`PyObjectManagedDict`、`PyTypeObject`、`PyExceptionType<>`、`UserDefinedType`、`PySharpException` 等。

## 文件编码

仓库内所有文本文件统一为 **UTF-8 无 BOM**，`.editorconfig` 的 `charset = utf-8` 是声明的正典。构建期以 `PYBOM001` 强制：`Directory.Build.targets` 里的 `EnforceNoBom` 目标用 `git ls-files` 枚举跟踪文件，任一文件以 `EF BB BF` 开头即报 error 并中断构建。

- 不依赖 `.editorconfig` 本身——`charset` 只驱动编辑器与 IDE，编译器不会强制，所以另配了构建检查。BOM 在评审中不可见，但编辑器重写它时会留下一个游离的首行 diff。
- 枚举走 `git ls-files`：既覆盖恰好随仓库交付的文件，也自动跳过被忽略的第三方树（`.reference/`、`.github/skills/`）——那些 CPython 参考源码里有刻意保留的带 BOM 与非 UTF-8 测试数据。
- **夹具的 BOM 不是规范允许项**：`test_pyfiles/` 的 `.py` 同样是 UTF-8 无 BOM。需要测 BOM 语义时在内存里构造字节（`b"\xef\xbb\xbf"`）或写自己的临时文件，不要让夹具文件自身带 BOM——那会让"测试数据"与"仓库编码纪律"纠缠在一起。
- 换行由 `.gitattributes` 的 `* text=auto` 归一，仓库内一律 LF；`PySharp/Lib/**/*.py` 另钉 `eol=lf`，因为其字节被逐字烘焙进生成代码。

## 控制流与大括号（PYSPS002 / 003 / 004 / 006）

- 单语句体不用大括号且必须换行：

  ```csharp
  if (result.IsError)
      return result;          // 正确
  if (result.IsError) return result;      // 错误：未换行
  if (result.IsError) { return result; }  // 错误：用了大括号
  ```

- 跨多行的裸语句体必须加大括号（PYSPS004）；if-else 链内的大括号风格必须一致（PYSPS003）。
- 使用 Allman 风格，`{` 独占一行（PYSPS006）；空块 `{ }` 同行豁免。
- 空类型体用表达式体声明（PYSPS007）：`class Foo;`。

## 表达式与惯用法

| 规则 | 要求 | 示例 |
| --- | --- | --- |
| PYSPS001 | 常量比较用模式匹配 | `obj is null`、`count is 0` |
| PYSPS005 | 用 `string.Empty` 代替 `""` | `PyStrObject.FromString(string.Empty)` |
| PYSPI001 | 内建命名空间内禁用 `__xxx__` 字面量 | 用 `PySpecialNames.Repr`；缺常量先去 `PySpecialNames` 定义 |
| PYSP003 | 用预驻留字段而非池查找 | `PySpecialNames.Interned.Repr` 而非 `InternPool.FromString(...)` |
| PYSP001 | 返回 `PyResult` 用隐式转换 | `return obj;` 而非 `return PyResult.FromValue(obj);` |
| PYSP002 | 比较器走上下文 | `context.Comparer` 而非 `PyObjectComparer.Default` |
| PYSP004 | 用常量代替工厂 | `PyIntObject.Zero`、`PyBoolObject.True`、`PyStrObject.Empty`、`PyFloatObject.NaN` |
| PYSP005 | 非泛型 `PyResult` 直接返回 | `return x;` 而非 `return x.ExceptionResult;`，后者会把成功值坍缩为 `None` |

## PyResult 惯例

- 协议实现统一返回 `PyResult` / `PyResult<T>`，错误经 `PyResult.TypeError(...)` 等工厂构造，格式串用 .NET 复合格式 `{0}`，消息资源取自 `PySR.*`。
- 不要在协议路径抛 `PyRuntimeException`，异常保留给帧边界（`PyCallContext` 的 Throwable 助手）与非 Python 的 .NET 错误。
- 消息文本进 `Resources`（`PySR`），不内联字符串字面量，便于统一措辞与本地化；组织方式与新增流程见[错误消息规范](./error-messages.md)。

## 类型对与源生成器约束

- 新类型遵循 `Py<Name>Object` 加 `Py<Name>ObjectType` 的配对加 `[PyType]`，模式见[用 C# 定义 Python 类型](../user-guide/custom-types.md)；方法用 `[PyMethod]` 时必须配 `[PyFunctionParameters]`，否则报 PYARG010，生成器无法免反射地生成描述符。
- 新协议槽登记到 `PyTypeObject.Declarations.cs`；新异常类型登记到 `PyExceptionObject.Types.cs`，自动获得 `PyResult.<Name>` 工厂。
- 大类型用 partial 分部组织，值类型拆 `.Py.cs` 存放 Python 侧方法等；`PyTypeObject` 系的分部拆分见[对象模型](../internals/object-model.md)。

## 其他工程惯例

- **AOT 纪律**：不引入运行时反射；新扩展能力优先考虑“特性加源生成器”的路线。
- **性能敏感路径**：优先 struct、`Span` 与池化，参照 `ValueOperandStack`、`PyArguments`、`ImmutableArrayBuilderPool` 的做法。
- **与 CPython 对齐**：语义不确定时对照 CPython 行为（错误类型、回退顺序、边界值），并在 `test_pyfiles` 落一个回归脚本。
- 提交信息用中文，`refactor:` / `fix:` 等 conventional 前缀加半角冒号，见[构建与测试](./build-and-test.md)。
