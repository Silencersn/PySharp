# 反汇编走查（Disassembly Walkthroughs）

以下指令流均为真实编译产物：经公共入口 `Compiler.Compile(code, CompileMode.Exec, context: null)`
编译后，读取 `PyCodeObject.Bytecode` 的指令数组取得，不是手工推导。

**读图约定**：

- `idx  指令名  Arg  (注解)`。Arg 为原始字节；引用常量池 / 名字池时以 `const[i]` / `name[i]` 展开；
- **跳转前缀**：连续三条 `ExtendedArg 0` + 跳转指令是一个**逻辑单元**（回填策略恒定 3 前缀，见[总览](./README.md#指令格式与编码)），下文以 `⋯` 前缀行合并展示；
- 尾部 `__BytecodeEnd` 对齐填充以省略号注记；
- 逐条语义查[各家族指令参考](./README.md#指令集参考)。

## 1. 常量折叠：`x = 1 + 2 * 3`

```
x = 1 + 2 * 3
-------- bytecode --------
code '<module>'  StackSize=1  consts=1  names=1
    0  LoadConst      0    const[0] = 7     ← 不是 LoadConst 1 + LoadConst 2 + …
    1  StoreGlobal    0    name[0] = 'x'
  ...（尾部 __BytecodeEnd 填充）
```

三个看点：纯字面量表达式在 **AST 层折叠**为单个常量（见[总览 · 编译期常量折叠](./README.md#编译期常量折叠)）；模块层代码用 **Global 族**存储（模块帧无 locals，变量分类的结果）；模块层连名字查找都走 `LoadGlobal` / `StoreGlobal`，没有 `LoadName`。

## 2. 分支：`if / else`

```
if x > 0:
    y = 1
else:
    y = 2
-------- bytecode --------
code '<module>'  StackSize=2  consts=3  names=2
    0  LoadGlobal         0   name[0]='x'
    1  LoadConst          0   0
    2  CompareOp          4                       ← CmpopType 4 = '>'
    3  ToBool             0                       ← 条件布尔化（窥孔未省略：前值是 CompareOp）
  ⋯  PopJumpIfFalse      14                       ← 假 → 跳 else 块
    8  LoadConst          1   1
    9  StoreGlobal        1   name[1]='y'
  ⋯  Jump                16                       ← then 块尾跳过 else，汇合点
   14  LoadConst          2   2
   15  StoreGlobal        1   name[1]='y'
```

`CompareOp` → `ToBool` → `PopJumpIfFalse` 的三连是条件的标准形态；两个 `StoreGlobal 'y'` 即 then / else 各自的赋值，`Jump 16` 保证只走其一。

## 3. 循环：`for`

```
total = 0
for i in range(3):
    total += i
-------- bytecode --------
    0  LoadConst          0   0
    1  StoreGlobal        0   name[0]='total'
    2  LoadGlobal         1   name[1]='range'
    3  LoadConst          1   3
    4  Call               1                       ← range(3)
    5  GetIter            0                       ← 可迭代 → 迭代器（原地替换）
  ⋯  ForIter             19                       ← 循环头：耗尽 → 出口 19；成功压下一项
   10  StoreGlobal        2   name[2]='i'
   11  LoadGlobal         0   name[0]='total'
   12  LoadGlobal         2   name[2]='i'
   13  _AugAssignOp       0                       ← OperatorType 0 = '+'（就地优先）
   14  StoreGlobal        0   name[0]='total'
  ⋯  Jump                 6                       ← 回边：回到 ForIter
   19  PopIter            0                       ← 出口：丢弃迭代器
  ...（尾部填充）
```

`GetIter → [ForIter …体… Jump 回边] → PopIter` 的环结构；增强赋值是"读-算-写"三段（`_AugAssignOp` 只负责算）。

## 4. 异常：`try / except / finally`

> 控制流区域重构（`EmitterRegion`）后，`except E as e` 块体有了**自己的名字清理处理器**，且块尾隐式清理是“先赋 `None` 再 `del`”两步。

```
try:
    risky()
except ValueError as e:
    handle(e)
finally:
    done = True
-------- bytecode --------
code '<module>'  StackSize=2  consts=2  names=5
  ⋯  _SetupFinally     12                      ← 安装处理器（finally 偏移 12）
  ⋯  _SetupExcept      20                      ← 登记 except 匹配区偏移
    8  LoadGlobal      0   name[0]='risky'
    9  Call            0
   10  PopTop          0
   11  _ClearExcept    0                       ← 体正常结束：撤销 except 登记
   12  _EnterFinally   0                       ┐
   13  LoadConst       0   const[0]=True       │ finally 块
   14  StoreGlobal     1   name[1]='done'      │
   15  _ExitFinally    0                       ┘ 收口（挂起物在此交接）
  ⋯  Jump              50                      ← 正常路径跳过 except 区
   20  LoadGlobal      2   name[2]='ValueError'   ┐ except 匹配区
   21  CheckExcMatch   0                          │（异常时从 20 进入）
  ⋯  PopJumpIfFalse  12                          │ 不匹配 → 进 finally（异常挂起）
   26  _LoadExc        0                          │ e ← 当前异常
   27  StoreGlobal     3   name[3]='e'            │
    ⋯  _SetupFinally   45                          ← except 块体自带"名字清理"处理器
   32  LoadGlobal      4   name[4]='handle'
   33  LoadGlobal      3   name[3]='e'
   34  Call            1
   35  PopTop          0
   36  LoadConst       1   const[1]=None         ┐ 隐式清理两步（CPython 语义）：
   37  StoreGlobal     3   name[3]='e'           │ e = None
   38  DeleteGlobal    3                          ┘ del e
   39  _ExitFinally    0                          ← 名字清理收口
   40  _PopException   0                          ← 异常已处理，弹传播链
  ⋯  Jump             12                         → finally
   45  _EnterFinally   0                          ┐ 名字清理处理器本体（块内再出
   46  LoadConst       1   const[1]=None          │ 异常时进入）：e = None、del e
   47  StoreGlobal     3   name[3]='e'            │ 后 _ExitFinally 交接，异常继续
   48  DeleteGlobal    3                          │ 由外层处理器传播
   49  _ExitFinally    0                          ┘
  ⋯  Jump             0
   50  __BytecodeEnd   0
```

对照[异常与模式匹配篇](./instructions-exceptions.md)的状态机骨架：**正常路径**沿 11 → 12–15 直落收口；**异常路径**由 VM 送入 20 起的匹配区，`CheckExcMatch → PopJumpIfFalse` 决定命中或穿透。except 块体额外包一层 `_SetupFinally 45`（名字清理处理器，`EmitterRegion.ExceptName` 的产物），块体再出异常时仍保证 `e = None; del e` 执行，异常继续传播。`_PopException` 与 `_PopFinally`（后者见第 8 篇的区域展开）分别是"块内正常收尾"与"非局部跳出"两条弹出路径。

## 5. 推导式：内联帧

```
squares = [n * n for n in range(5)]
-------- bytecode --------
    0  BuildList          0                       ← 空列表（收集器）
    1  _EnterInlineFrame  0                       ← 推导式独立帧形态
    2  LoadGlobal         0   name[0]='range'
    3  LoadConst          0   5
    4  Call               1
    5  GetIter            0
  ⋯  ForIter             19
   10  StoreGlobal        1   name[1]='n'
   11  LoadGlobal         1   name[1]='n'
   12  LoadGlobal         1   name[1]='n'
   13  BinaryOp           2                       ← OperatorType 2 = '*'
   14  ListAppend         2                       ← 追加到栈顶下第 2 项（列表在迭代器之下）
  ⋯  Jump                 6
   19  PopIter            0
   20  _ExitInlineFrame   0                       ← 回到外层帧
   21  StoreGlobal        2   name[2]='squares'
```

`ListAppend 2` 的深度参数即"列表在迭代器之下"的栈布局实证（见[跳转与容器构造](./instructions-control-flow.md#listappend--追加列表元素)）；`_EnterInlineFrame` / `_ExitInlineFrame` 包裹整个循环体（见[格式化与杂项](./instructions-misc.md#_enterinlineframe--进入推导式内联帧)）。

## 6. f-string：格式化流水线

```
msg = f'a={a!r:>6}'
-------- bytecode --------
    0  LoadConst          0   const[0]='a='       ← 字面量段
    1  LoadGlobal         0   name[0]='a'         ← 表达式段求值
    2  ConvertValue       2                        ← !r（repr）
    3  LoadConst          1   const[1]='>6'       ← 格式说明符
    4  BuildString        1                        ← 单段契约：顶已是 str，无操作
    5  FormatWithSpec     0                        ← format(value, '>6')
    6  BuildString        2                        ← 总装 'a=' + 格式化结果
    7  StoreGlobal        1   name[1]='msg'
```

[格式化与杂项](./instructions-misc.md)流水线的完整实例：`ConvertValue 2` 与 `FormatWithSpec` 各就各位，`BuildString 1` 的"无操作分支"在真实输出中的形态。

## 7. 函数定义：嵌套代码对象

```
def add(a, b=10):
    return a + b
-------- bytecode --------（模块层）
    0  LoadConst          0   10                  ┐ 位置缺省：值 + 组元组
    1  BuildTuple         1                       ┘
    2  LoadConst          1   tuple               ← 关键字缺省：空元组
    3  LoadConst          2   <code 'add'>        ← 函数体代码对象（常量池）
    4  _MakeFunctionWithPyArgsDef 0
    5  StoreGlobal        0   name[0]='add'
-> const[2] 嵌套代码对象：
    code 'add'  StackSize=2  consts=0  names=0
        0  LoadFast       0                      ← a：槽位 0（不是名字池！）
        1  LoadFast       1                      ← b：槽位 1
        2  BinaryOp       0                      ← '+'
        3  ReturnValue    0
      ...（尾部填充 ×4）
```

两代码对象的对照浓缩了[常量与名字篇](./instructions-loads.md)的核心：模块层全 `*Global`，函数体内局部名全 `*Fast`（按槽位下标，`names=0`，函数体的名字根本不进名字池）；缺省值三件套的栈布局见[函数、类与模块篇](./instructions-functions.md#_makefunctionwithpyargsdef--创建函数带签名)。

## 8. 区域展开：`break` 跳出 `with`

控制流区域重构（`EmitterRegion`，见[总览 · Emitter](./README.md#emitter)）的完整实证，`break` 同时跳出 `with` 与 `while` 两个区域：

```
while True:
    with open('data') as f:
        if check(f):
            break
-------- bytecode --------
code '<module>'  StackSize=6  consts=3  names=3
    0  LoadConst            0   const[0]=True      ← while 条件（恒真，形态完整）
    1  ToBool               0
  ⋯  PopJumpIfFalse       76                      ← 条件假 → 循环出口
    6  LoadGlobal           0   name[0]='open'
    7  LoadConst            1   const[1]='data'
    8  Call                 1                       ← open('data')：管理器
    9  LoadSpecial          0                       ← 取 __enter__ 描述符
   10  Swap                 2                       ┐ 旋转出 with 的驻留布局
   11  LoadSpecial          1                       │ [__exit__, manager]
   12  Swap                 3                       │
   13  Copy                 2                       ┘
   14  Call                 1                       ← manager.__enter__()
  ⋯  _SetupFinally       60                       ← with 处理器：异常 → 收口 60
  ⋯  _SetupExcept        51                       ← __exit__ 判定区：异常 → 51
   23  _ExcludeWithResult  1                       ← as 绑定值驻留栈上，校准处理器回滚基准
   24  StoreGlobal         1   name[1]='f'         ← f = __enter__() 结果
   25  LoadGlobal          2   name[2]='check'
   26  LoadGlobal          1   name[1]='f'
   27  Call                1
   28  ToBool              0
  ⋯  PopJumpIfFalse      47                       ← if 条件假 → 跳过 break
  ⋯  _PopFinally          1                       ← 区域展开第一步：弹出 with 处理器记录
   34  LoadConst           2   const[2]=None       ┐ 内联复制"清理体"：
   35  LoadConst           2   const[2]=None       │ f.__exit__(None, None, None)
   36  LoadConst           2   const[2]=None       │ （记录已弹，不能再走 60 的收口）
   37  Call                4                        ┘
   38  PopTop              0                        ← 丢弃 __exit__ 返回值
  ⋯  Jump                76                       ← break：直接跳出整个 while
  ⋯  Jump                47                       ← if 汇合补全（break 路径由 42 接管，不可达）
  ⋯  Jump                60                       ← with 体正常尾 → 走收口骨架
   51  _LoadExcInfo        0                        ┐ 异常路径：以 (type, exc, tb)
   52  Call                4                        │ 调用驻留的 f.__exit__(…)
   53  ToBool              0                        │
   54  _PopExceptionIfTrue 0                        │ 返回真 → 吞掉：弹传播链
  ⋯  PopJumpIfTrue       60                        │ 吞掉 → 进收口
   59  RaiseVarArgs        0                        ┘ 不吞 → 重抛（穿透）
   60  _EnterFinally       0                        ┐ with 的隐式收口骨架
   61  _LoadHitExcept      0                        │（语句没有显式 finally，
  ⋯  PopJumpIfTrue       71                        │  处理器状态机仍需这对收口）
   66  LoadConst           2   const[2]=None        ┐ 正常路径补调
   67  LoadConst           2   const[2]=None        │ f.__exit__(None, None, None)
   68  LoadConst           2   const[2]=None        │
   69  Call                4                         ┘
   70  PopTop              0
   71  _ExitFinally        0                        ← 弹处理器记录 + 挂起物交接
  ⋯  Jump                 0                       ← while 回边
   76  __BytecodeEnd        0                       ← 循环出口 == 程序尾
```

**核心看点：break 路径（33–42）与收口骨架（60–71）是互斥的两条清理通道。**`break` 跳出 `with` 时，`_PopFinally 1` 已把处理器记录弹出、`__exit__` 已在 34–38 内联调用，因此 break 路径**不能也不需要**进入 60 的收口骨架（那里 `_EnterFinally` / `_ExitFinally` 假定记录在栈上），`Jump 76` 直接跳到循环出口。发射器沿 `EmitterRegion` 区域栈展开时，把 with 的"清理体"（`__exit__` 调用）**内联复制**到 break 路径，即 `try/finally` 的 finally 体、`except E as name` 的名字删除同理。三条路径调用 `__exit__` 的对照：break 走 34–38（内联、无参）、正常尾走 66–69（收口骨架内补调）、异常走 51–52（exc_info 三参，返回真吞掉 / 假重抛）。

其余看点：`_ExcludeWithResult 1` 把处理器的回滚基准下调一个栈位，因为 `as` 绑定值在受保护体期间驻留栈上，异常回滚不能把它清掉（`[__exit__, manager]` 对必须原样留给收口代码）；51–59 的"吞 / 穿透"判定是 `with` 异常语义的指令端（`__exit__` 返回真 → `_PopExceptionIfTrue` 吞掉传播链）。回归族：`test_with_break_continue_regression.py`、`test_finally_control_flow_regression.py`。

## 相关阅读

[字节码总览](./README.md)（格式与发射管线）· 各[家族指令参考](./README.md#指令集参考) · [虚拟机](../virtual-machine.md)（这些指令的执行侧）
