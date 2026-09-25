# 贡献流程

本分区收录参与开发的过程性文档：如何构建与验证、如何写合规代码、如何完成一类常见任务、发布怎么走、还有哪些可做。理解实现本身请进 [internals](../internals/README.md)。

## 按任务进入

| 你要做的事 | 先读 |
| --- | --- |
| 跑起来、验证改动 | [构建与测试](./build-and-test.md) |
| 新增内建类型、标准库模块、异常、协议槽或内建函数 | [新增类型与模块：操作指南](./adding-types-and-modules.md) |
| 排查 bug、定位是哪一层的问题 | [调试指南](./debugging.md) |
| 新增或修改错误消息 | [错误消息规范](./error-messages.md) |
| 改公共 API、判断能不能依赖某个成员 | [公共 API 稳定性政策](./api-stability.md) |
| 发版 | [发布流程 checklist](./release-process.md) |
| 找可认领的工作 | [路线图与可认领缺口](./roadmap.md) |

## 篇目一览

上手篇目是[构建与测试](./build-and-test.md)，覆盖环境、命令、按改动类型的验证路径与版本提交惯例；以及[编码规范](./coding-standards.md)，覆盖 PYSPI\* / PYSP\* 强制规则、`PyResult` 惯例与 AOT 纪律。

任务操作指南包括[新增类型与模块](./adding-types-and-modules.md)（五类任务的分步清单与收尾 checklist）、[调试指南](./debugging.md)（分层定位、断点建议、读生成的 `.g.cs`）和[错误消息规范](./error-messages.md)（`PySR` 的组织、命名与新增流程）。

政策与治理类文档包括[公共 API 稳定性政策](./api-stability.md)（public / internal 边界、三个刻意设计的边界、新公共 API 准入）、[发布流程 checklist](./release-process.md)（打包结构、顺序敏感点、发布后验证）和[路线图与可认领缺口](./roadmap.md)（四档缺口地图与认领指南）。

## 最低上手集

时间有限时读三篇即可开工：构建与测试保证能验证，编码规范保证能过构建，新增类型与模块覆盖最高频任务的清单。遇到故障再查调试指南，改公共面前先查稳定性政策。
