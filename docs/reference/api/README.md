# API 参考

本分区是核心公共 API 的逐成员参考，用于查阅「某个类型有哪些公共成员、各是什么语义」。
指南型内容（如何组合使用）在 [user-guide](../user-guide/README.md)。

## 篇目索引

| 篇目 | 覆盖类型 |
| --- | --- |
| [PyInterpreter](./PyInterpreter.md) | `PyInterpreter`：静态便捷方法与实例方法、REPL 行为、`__main__` 共享语义 |
| [PyEnvironment](./PyEnvironment.md) | `PyEnvironment`、`IPyEnvironmentBuilder`、`PyEnvironmentOptions` |
| [PyEnvironmentHost](./PyEnvironmentHost.md) | `PyEnvironmentHost`、`IPyEnvironmentHostBuilder`：宿主抽象、预定义宿主、I/O 分配时机 |
| [PyFileSystem](./PyFileSystem.md) | `IVirtualFileSystem`、`IVirtual*Info`、`MemoryFileSystem`、`PhysicalFileSystem` |
| [PyObject](./PyObject.md) | `PyObject`、`PyObjectManagedDict`、`Call` 与 `CallMethod` 扩展 |
| [PyResult](./PyResult.md) | `PyResult`、`PyResult<T>`、`PyExceptionResult`：错误即值、状态判定、异常工厂 |
| [PyOperators](./PyOperators.md) | `PyOperators`、`PyOperatorTypes`：运算符入口与分发规则 |
| [PySpecialMethods](./PySpecialMethods.md) | `PySpecialMethods`：协议方法入口与回退规则 |
| [内建类型速查表](./builtin-types.md) | 内建类型到构造入口的对照、异常类型体系、执行与迭代构件 |

## 阅读提示

- 公共并不等于随处可用：`PyOperators`、`PySpecialMethods` 与调用扩展都需要 `PyCallContext`，
  它仅在扩展点内提供，见 [PyObject 参考](./PyObject.md)与
  [对象互操作](../user-guide/python-objects-from-csharp.md)。
- public 与 internal 的边界政策见[公共 API 稳定性政策](../contributing/api-stability.md)，
  判断某个成员能否依赖时应先查它。
- 未列入本分区的 `Py*Object` 类型遵循同一模式（工厂构造、读取访问器、协议覆写），
  可参照速查表与[自定义类型](../user-guide/custom-types.md)推断。
