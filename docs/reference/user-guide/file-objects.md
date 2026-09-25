# 文件对象

源码：`PySharp/Modules/IO/PyFileObject.cs`、`PySharp/Modules/Builtins/PyBuiltinFunctions.cs`
（`open`）。C# 侧类型 `PyFileObject`，Python 类型名 `_io.FileObject`。

`open()` 在环境的[虚拟文件系统](./virtual-file-system.md)上打开文件并返回文件对象。本篇说明该对象
的 Python 侧方法面，供脚本作者与需要核对行为的嵌入者查阅。

## `open(file, mode='r', buffering=-1, encoding=None, errors=None, newline=None)`

- `file`：路径字符串，相对路径基于文件系统的 `CurrentDirectory` 解析；非字符串报 `TypeError`。
- `mode`：由 `r`、`w`、`a`、`x`、`b`、`t`、`+` 组成。校验规则与 CPython 一致：必须恰好包含一个
  `r`、`w`、`a`、`x`，零个主模式（如 `''`、`'b'`、`'+'`）或多个主模式（如 `'rw'`）都报
  `ValueError`；出现其他字符或重复字符同样报 `ValueError`。
- `buffering`：`-1` 或 `0`（二进制）表示默认行为。二进制模式下 `buffering=0` 合法，
  文本模式下 `buffering=0` 报 `ValueError`。
- `encoding`：文本模式下的编码名，默认 `utf-8`。二进制模式传入 `encoding` 报 `ValueError`。
- `errors`：文本模式下的编解码错误处理器名，默认 `strict`。
- `newline`：文本模式下的换行策略，取 `None`、`''`、`'\n'`、`'\r'`、`'\r\n'` 之一，其他值报
  `ValueError`。

| 模式 | 含义 | 不存在时 | 已存在时 | 可读 | 可写 |
| --- | --- | --- | --- | --- | --- |
| `r`、`rt` | 读（默认） | `FileNotFoundError` | 从头读 | 是 | 否 |
| `r+` | 读并写 | `FileNotFoundError` | 从头读写 | 是 | 是 |
| `w`、`wt` | 写 | 新建 | 截断 | 否 | 是 |
| `w+` | 写并读 | 新建 | 截断 | 是 | 是 |
| `a`、`at` | 追加 | 新建 | 写永远到尾部 | 否 | 是 |
| `a+` | 追加并读 | 新建 | 打开时定位到尾部，读写皆可 | 是 | 是 |
| `x`、`xt` | 独占创建 | 新建 | `FileExistsError` | 否 | 是 |
| `x+` | 独占创建并读写 | 新建 | `FileExistsError` | 是 | 是 |

上述任意模式加 `b` 为二进制变体（`rb`、`wb+` 等），读写单位从 `str` 变为 `bytes`。

错误映射对齐 CPython：目标不在存在的目录中、权限不足、其他进程持有写锁（共享违规），分别报
`FileNotFoundError` 或 `PermissionError`；目标本身是目录时按平台区分，Windows 报 `PermissionError`，
其他平台报 `IsADirectoryError`。.NET 侧的 IO 异常都会被转成可捕获的 Python 异常，不会泄漏原始的
`IOException`。

## 方法

| 方法 | 签名 | 行为 |
| --- | --- | --- |
| `read` | `read(size=-1)` | 文本模式返回 `str`，最多读取 `size` 个字符；二进制返回 `bytes`，最多 `size` 个字节。`size < 0` 读到 EOF，`size = 0` 返回空。不可读的句柄报 `ValueError` |
| `readline` | `readline(size=-1)` | 读一行，含行尾 `\n`，最后一行可以没有行尾。`size` 限制单行最大读取量。EOF 处返回空值 |
| `readlines` | `readlines(hint=-1)` | 逐行读到 EOF，返回 `list`。`hint > 0` 时累计超过 `hint` 即停，跨过阈值的那一行保留，与 CPython 的 `_IOBase.readlines` 一致。文本按字符计数，二进制按字节计数 |
| `write` | `write(data)` | 文本模式只接受 `str`，二进制只接受 `bytes`，否则报 `TypeError`。返回写入的字符数或字节数。写入后立即落盘（内部 flush） |
| `seek` | `seek(offset, whence=0)` | `whence` 取值 `0` 为文件头（`offset` 不可为负）、`1` 为当前位置、`2` 为尾部。实参经 `__index__` 归化。非法 `whence` 报 `ValueError`；文本模式下非零相对定位报 `_io.UnsupportedOperation` |
| `tell` | `tell()` | 返回当前位置 |
| `close` | `close()` | 关闭句柄，重复调用为幂等的空操作 |
| `flush` | `flush()` | 冲刷内部缓冲 |
| `readable`、`writable`、`seekable` | 无参 | 按打开模式返回 `True` 或 `False` |

属性有 `closed`（bool）、`mode`（打开时的模式串）、`name`（路径字符串）。文本模式下另有
`encoding` 与 `errors`，二进制模式下访问这两个属性报 `AttributeError`。关闭后的 I/O 操作
（`read` 族、`write`、`seek`、`tell`、`flush`、迭代）均报
`ValueError: I/O operation on closed file`；`close` 幂等，`closed`、`mode`、`name`、
`readable`、`writable`、`seekable` 等查询不受关闭影响。

## 迭代与上下文管理器

```python
with open("data.txt") as f:        # __enter__ 返回自身，__exit__ 保证 close
    for line in f:                  # 迭代等价于循环 readline，EOF 抛 StopIteration
        process(line)

lines = open("data.txt").readlines()
```

迭代以行为单位，包含行尾换行符。`with` 语句在块退出时关闭文件，异常路径同样关闭，见
[错误处理](./error-handling.md)。

## 文本与二进制模式

- 文本模式：`read`、`readline`、`readlines` 返回 `str`，`write` 接收 `str`。
  `newline=None` 为默认模式，读入时把 `\r\n` 与 `\r` 归一为 `\n`（通用换行），写出时把 `\n`
  展开为该平台的换行符；显式指定 `newline` 时按该值处理，`''` 与 `'\n'` 不翻译。
- 二进制模式：上述方法返回或接收 `bytes`。
- 文本模式的编码由 `encoding` 决定，默认 UTF-8，不写入 BOM；`errors` 决定编解码错误的处理方式。
  二进制模式下需要其他编码时，在 Python 侧对 `bytes` 手工 `decode` 与 `encode`。

## C# 侧

`open()` 的返回值在 C# 中是 `PyFileObject`（命名空间 `PySharp.Modules.Builtins`），见
[内建类型速查表](../api/builtin-types.md)。宿主代码通常不直接实例化它；需要从 C# 侧写文件喂给脚本时，
直接操作 `IVirtualFileSystem` 更简单，见[虚拟文件系统](./virtual-file-system.md)。

## 相关节点

- [虚拟文件系统](./virtual-file-system.md)：`open()` 与 `import` 背后的存储层
- [宿主与 I/O 重定向](./hosting.md)：stdin、stdout、stderr 的包装，`sys.stdin` 等是另一套流对象
- [标准库模块覆盖](../python-compat/stdlib-modules.md)：`open` 所属的内建函数清单
