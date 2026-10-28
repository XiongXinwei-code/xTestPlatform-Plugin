# 插件修改说明与交付边界

本轮按开发手册统一关闭结果与历史业务故障的关系，并重新编译全部十个插件及 UI。最终留存目录为 `artifacts/Release/2026-10-27-final-plugins/Plugins/`，各子文件夹是独立部署单元；核心、UI 和私有依赖须成套替换。编译验证时没有修改已部署的平台文件，源码提交状态以 Git 为准。

## 本轮修改及原因

| 插件 | 版本 | 关键修改 |
| --- | --- | --- |
| CAN / CANopen | 1.0.49 | 通用周期任务停止跟踪本任务 SDK 写入的真实退出。软超时不冒充实际退出；已退出的历史错误输出警告，释放旧任务名。停止一个任务不等待无关任务，不因纯托管等待超时隔离整个连接。既有 PDO/SYNC 发送、SDO、适配器及 UDS 行为不改。 |
| CiA402 | 1.0.14 | 更新私有 CAN 依赖，轴控制与解绑行为不改。解绑仍可能执行配置的停轴/释放使能；其成功不能由父 CAN 已关闭替代。 |
| Ethernet | 1.0.26 | 本轮未新增逻辑改动，按当前源码重新编译；TCP/UDP 按自己的连接实例和实际 I/O 退出收尾。 |
| HTTP | 1.0.27 | 请求、取消回调与 Dispose 分别等待。实际释放成功后，已退出回调异常保留警告；真实 Dispose 失败继续报错，不能用二次空操作假报成功。移除登记后再输出诊断。 |
| LIN | 1.0.35 | 修复物理已关闭仍因旧周期错误返回 Error。周期 Stop 等本任务真实写入和回调退出，不关闭父连接；收尾按配置总期限核对。 |
| Modbus | 1.0.26 | RTU/TCP 的本次真实释放结果与已退出中断异常分账；合法释放完成后历史错误保留警告，真实未退出/释放失败仍保留原实例。 |
| NI-DAQ | 1.0.27 | 非 AO 的 Abort 返回错误后，实际 I/O 退出仍继续尝试合法 Dispose，避免 Abort 失败阻止最终释放。真实 Task.Dispose 失败缓存，不信任第二次 no-op；AO 指定终值、未知输出与真实 writer 退出约束保留。 |
| OPC UA | 1.0.27 | 使用固定 SDK 的 CloseAsync(false, ct) 将远端关闭和本地 Dispose 分开，防止隐式 Dispose 后再重复释放。远端/历史取消错误保留警告，真实 Dispose 失败缓存；诊断日志不作为真实回调退出前置，父连接保留子采集故障。 |
| SerialPort | 1.0.35 | 已退出中断/取消异常不覆盖真实 Dispose 成功；先确认 I/O、回调和最终释放，再移除精确实例。 |
| VISA | 1.0.27 | 保存首次真实 Dispose 失败及原句柄。部分 SDK 已标记 disposed 后的第二次空操作不能证明句柄释放，不再误报成功。 |

关闭步骤依赖自己的连接资源，独立冻结并收尾所拥有的后台工作，不要求此前业务步骤成功或先执行子任务停止。子任务停止只释放自己的资源。真实原生调用尚未退出、最终 SDK 释放失败或本次配置期限届满时，步骤仍返回明确 Error；这类失败不能通过清空资源表伪装成可以重开。

本轮不新增 Setting 字段、默认值、枚举、步骤标识或树路径；行为 Description、README、CHANGELOG 与核心/UI 版本同步。开发手册为 3.4.0，修改前已核对远端 master 与本地 Git blob 均为 9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0。配置期限始终采用步骤值；只有原调用方取消按 Aborted 返回，内部期限与释放失败保留中文 ErrorInfo 和异常链。

## 已保留的功能优化

- CAN、CANopen、CiA402 共用连接和唯一硬件接收泵，原始 CAN 观察、协议事务及各轴反馈获得独立副本。SDO 使用 CANopen 步骤，原始 CAN 补充其它报文。
- 多个 SYNC 源独立周期、生命周期；PDO/SYNC 默认只提交，RPDO→SYNC 整批 FIFO 不拆分，读取独立。反馈数量可配置，保留设备原始时间及可信采集时间，不用出队时刻刷新旧帧。
- 后台不可变轨迹点表支持迟到窗口、跳点追赶与统计；轨迹结束或命令提交均不表示电机到位或停稳。运动停止按捕获代次收尾，保留其它监测源。
- NI-DAQ 保留 AO 电压配置和单点输出，初值、终值与收尾策略由用户配置。
- 全插件保留执行代次、精确实例移除、编辑器保存与功能树分层，避免旧步骤取消或收尾影响新资源。

## 验证与限制

最终软件测试、Release 构建、120 个步骤身份/树路径、二进制来源、Windows RID 资产与私有 CAN 一致性，以交付目录 verification 的本轮记录为准。新增故障用例先保留失败回执，再验证修复；独立只读复审与复测结论见 [本轮计划与验收](plans/2026-10-27-close-outcome-independence.md)。

真实板卡、OPC UA 服务器、仪器、完整平台中断后立即重跑及调试退出重入没有现场验证。假适配器/组件测试与编译不能证明真实时序、设备断线恢复、电气输出或电机停稳；覆盖率未测量。真实 SDK 释放失败的未知状态不能承诺自动恢复。本轮不通过强清句柄、伪造发送确认或默认输出掩盖问题。

## 主要源码入口

- CAN：CanPlugin/Runtime/CanConnectionResource.cs、Executors/CanCyclicSendStopExecutor.cs；LIN：LinPlugin/Runtime/LinConnectionResource.cs、LinCloseExecutor.cs、LinCyclicSendStopExecutor.cs；共用的真实写票据位于 Shared/Runtime/NativeWriteLifetime.cs。
- HTTP：HttpPlugin/Helpers/HttpHelper.cs；Modbus：ModbusPlugin/Helpers/ModbusConnectionResource.cs；串口：SerialPortPlugin/Helpers/SerialConnectionResource.cs，以及各自关闭执行器。
- NI-DAQ：NiDaqPlugin/Helpers/DaqTaskResource.cs、Executors/NiDaqTaskStopExecutor.cs；OPC UA：OpcUaConnectionResource.cs、OpcUaDataAcqTask.cs、新增 OpcUaSessionShutdown.cs 及断开/采集停止执行器；VISA：VisaPlugin/Helpers/VisaSessionResource.cs。
- 配套核心/UI csproj、步骤 Description、README、CHANGELOG 同步；新增及扩展的 CloseOutcome、NativeWriteStop、CloseDiagnosticBoundary 行为测试保留在相应测试项目。
