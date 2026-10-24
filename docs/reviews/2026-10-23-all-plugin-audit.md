# 全项目插件风险审查（2026-10-23）

## 结论与边界

任务等级：L3。审查当前保存的源码，包括 CAN 1.0.33 和此前未提交的 CAN/CiA402 修改；基线分支 `master`，HEAD `5351a0a9d66594455b292fe65308303488a07537`。不是对远端 master 或已部署 DLL 的认证。

主要问题不是代码风格，而是：**步骤返回不代表底层 I/O 退出、后台任务没有可确认的生命周期、无效/陈旧/不匹配的数据可以进入成功路径，以及 UI 配置与执行语义不一致。** 建议先修生命周期和错误成功判定，再处理配置与性能。

本轮只读审查生产代码，未实施下列修复，未修改实际序列、设备、驱动、版本或 Git 引用。新增本报告、审查计划和隔离诊断探针。未重新拉取或覆盖已有工作区。

证据分级：

- **离线复现**：执行当前源码/当前构建程序集的纯软件、假适配器或回环探针，观察到明确错误行为；不等于硬件复现。
- **源码确认**：有具体触发条件及调用链；相关设备时序、驱动错误码和界面实际操作未验证。
- **条件风险/优化**：依赖宿主或非可信输入等额外条件，不作为无条件已复现缺陷。

P1 表示建议优先阻断的设备通信/输出/判定正确性问题；P2 表示其他明确的功能、资源和可靠性问题。未执行可能分配约 2 GB 的恶意长度实验，也未执行任何真实电机、PLC、ECU 或仪器动作。

## 覆盖范围

| 插件 | 重点审查内容 | 主要发现 |
| --- | --- | --- |
| CAN | 连接所有权、共享 CANopen 路由、周期服务、NI/ZLG 及其他适配器、ISO-TP/UDS、Flash/XCP、设置与 UI | F03–F06、F13、F28–F29 |
| CiA402 | 会话/绑定/解绑、协调传输、控制器、被动 TPDO、反馈映射与 UI 保存 | F03 跨插件链路；未把已有生命周期修复重新列成未修问题 |
| Ethernet | TCP 池、DoIP 请求/关闭、SOME/IP 编解码与请求、发现/通知 | F10–F11、F30 |
| Http | 客户端创建/替换、请求、SOAP、JSON/XML 提取、认证/TLS/日志 | F26–F27、R02 |
| LIN | NI 适配器、打开/关闭、读写及周期任务、校验设置与 UI | F02、F12、F23 |
| Modbus | NModbus 事务/超时、连接清理、批量读写、格式与 UI 映射 | F01、F17–F18 |
| NiDaq | 注册表、DAQmx 任务/超时、AI/CI 同步读写、停止与文件导出 | F01、F07–F08、F24–F25 |
| OpcUa | Session、单/批读写、订阅、后台采集、设置保存 | F09、F19–F22 |
| SerialPort | 打开/关闭、终止符组帧、Query 事务、取消与输出 | F01、F16 |
| VISA | Session、I/O 锁、超时/关闭、Query/批量写、WaitOPC | F01、F14–F15 |

库存扫描得到 10 类插件、20 个核心/UI 项目、2 个测试项目；928 个 C# 文件，约 5.42 万行（含测试）。审查采用入口到资源/协议的调用链阅读，并非声称每个文件每一行、全部 XAML 交互及所有厂商 ABI 都已经逐项验证。纯图标/模板资源和第三方二进制不做全量审计。

## P1：优先处理

### F01．VISA / Modbus / NI DAQ / SerialPort：软超时或取消后仍允许复用、释放原始 I/O 资源

证据：**辅助方法行为已离线复现；完整驱动交错为源码确认。**

- [VISA 辅助方法](/D:/project/xTestPlatform_Plugin/VISA/VisaPlugin/Helpers/VisaHelper.cs:93) 通过 `Task.Run` 执行同步 I/O，`WaitAsync` 只结束等待。[Query finally](/D:/project/xTestPlatform_Plugin/VISA/VisaPlugin/Executors/VisaQueryExecutor.cs:55) 随即释放会话锁；Close 可移除/释放尚被后台调用使用的 Session。
- [Modbus 辅助方法](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin/Helpers/ModbusHelper.cs:34) 接收已经启动的 `Task`，超时没有等待其退出或隔离连接。[写执行器](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin/Executors/ModbusWriteExecutor.cs:64) 在辅助方法检查取消之前就调用 NModbus；已取消的步骤也可能先提交写操作。
- [NI DAQ 辅助方法](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/NiDaqTimeoutHelper.cs:15) 同样只取消等待；[DO 写入](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqDoWriteExecutor.cs:28) 的 `using` 随即释放仍可能在使用的 DAQ Task。
- [串口辅助方法](/D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin/Helpers/SerialPortHelper.cs:189) 未跟踪取消后仍阻塞的同步 Read；[Query](/D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin/Executors/SerialPortQueryExecutor.cs:47) 没有覆盖 Write→Read 的事务门。旧 Read 可能消费新请求的回包。

影响：Error/Aborted 不等于设备命令取消，可能出现晚到输出、旧读窃取新应答、重叠调用或 Dispose 与 native I/O 并发。不能由此断言 NModbus 内部一定并发发送，但插件没有真实退出保证。

建议：由连接资源持有事务门、pending 调用及关闭状态；锁和资源生命周期跟随真实调用完成，而不是跟随等待者。超时/取消后若不能确认退出，保持隔离与引用，按 SDK 支持的 Abort/Stop 解除阻塞，再验证退出。不得静默关闭后报告“已取消发送”。

验收：阻塞式假驱动在等待取消后仍活动时，禁止后续事务/释放；确认退出后才允许恢复。覆盖已取消令牌入参、晚到写完成、关闭失败和原始异常传播。

### F02．LIN 周期停止只取消 CTS，未确认发送任务退出

证据：**源码确认。** [启动](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Executors/LinCyclicSendStartExecutor.cs:60) 仅注册 CTS，丢弃所有 worker Task；[停止](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Executors/LinCyclicSendStopExecutor.cs:25) 只等待 `CancelAsync()` 就报告成功。同名重启也不等待旧 worker。

触发：worker 已进入表达式求值或 `adapter.Write()` 时 Stop/重启/Close。已进入当次循环的任务仍可发送；NI Write 与 Close 的 `nxClear` 没有共同 I/O 生命周期协调。

建议：连接拥有任务组；停止先冻结新发布，再取消、等待 worker/native I/O 实际退出。超时保留隔离，不报告已停止。后台故障应供状态步骤查询，不能仅日志后任务消失。

验收：暂停在求值和 Write 内的 fake worker，验证 Stop 不会提前成功、同名重启无旧帧、Close 不会先销毁句柄。

### F03．CANopen 非受控接收路径把积压 TPDO 当作新鲜反馈

证据：**离线复现。** [CanopenConnectionAccess](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/CANopen/CanopenConnectionAccess.cs:143) 从适配器取出帧后，用当前 `Stopwatch` 构造 `CanopenReceivedFrame`；其 `QueueAge` 忽略已有 `IngressMonotonicTicks`/可信采集下界。[CiA402 反馈](/D:/project/xTestPlatform_Plugin/CiA402/Cia402Plugin/Transport/CanopenAxisTransport.cs:129) 据此判定过期。

探针：入口时间已过去约 10010 ms 的帧返回 `QueueAge≈19 ms`，能通过 100 ms 新鲜度门槛。整组旧帧内部时间跨度很小，组间跨度检查不能识别整体陈旧。

范围：这里指非受控 CANopen 路径，不是说受控快照路由全部失效。TPDO 路数应按序列配置，不假定固定四路。

建议：保留最早入口时间；涉及采集新鲜度时使用可信时钟映射及保守下界，没有可信时间则保持未知，不以出队时刻刷新。每路 TPDO 独立判断有效期。

验收：适配器积压、共享 pending 队列积压、时间未知、整组陈旧但组内同步、任意配置 TPDO 数量。

### F04．UDS 把错误 DID/RID/下载块序号或不完整正响应判为成功

证据：**离线复现。** [UdsClient 校验](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/UDS/UdsClient.cs:161) 只验证 SID 和部分子功能；[ReadDataById](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/UDS/Executors/UdsReadDataByIdExecutor.cs:31) 在 `IsPositive` 后直接取值；[Flash](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Flash/Executors/CanFlashExecutor.cs:168) 直接推进块计数。

探针：请求 `22 F1 89`，响应仅 `62` 或 `62 F1 90` 均为 `IsPositive=True`；请求 `36 01 AB`，收到 `76 99` 也为真。

影响：读取错误对象、下载计数在错误 ACK 下推进，RoutineControl 同子功能不同 RID 也缺少关联。`ClaimRawProtocol()` 是访问模式标记，不是多个 UDS 客户端之间的事务互斥。

建议：按服务验证最小长度及 DID/RID/blockSequenceCounter 等回显；连接级维护完整 UDS 事务归属，统一处理迟到响应。不把任意正 SID 当成功。

验收：上述反例、相同 SID 的交错请求、错误 RID、短响应、NRC 0x78 和取消后的迟到响应。

### F05．Flash 地址/长度超出 ALFID 位宽时静默截断

证据：**编码离线复现；错误擦除报文路径源码确认，未发送真实擦除。** [位宽检查](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Flash/Executors/CanFlashExecutor.cs:84) 只检查 1～4 字节；[编码](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Flash/Executors/CanFlashExecutor.cs:265) 丢弃高位。

例如地址 `0x02000000` 配 ALFID `0x41`（地址 1 字节），编码变成 `00`，会用于擦除 option record 和 `0x34` 请求。ECU 是否拒绝取决于设备，不能依赖它来保证本地安全。

建议：任何擦除之前，预检全部数据段起址、长度及地址范围能被 ALFID 精确表示；溢出直接报错且不发送任何写/擦除请求。

### F06．ISO-TP 后续 FC 忽略 Wait/Overflow 及新 BS/STmin

证据：**离线复现。** [后续 FC](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/UDS/IsoTp/IsoTpTransport.cs:192) 只检查高四位。首块后收到 `31` Wait 或 `32` Overflow 仍继续 CF；不更新 BS/STmin，空 Data 可越界。

探针：20 字节 Classic 请求、首 FC=`30 01 00`，后续 FC=`31 00 00`/`32 00 00`，均仍发送第二个 CF。

建议：首个/后续 FC 复用完整状态机和长度校验；Wait 有等待上限，Overflow 终止，合法 CTS 应采用新的 BS/STmin。覆盖取消边界。

### F07．NI DAQ“同步采集”的任务结构和数据不能表示同步 AI/编码器采样

证据：**源码与官方契约对照，未进行 DAQmx/硬件运行。** [SyncConfig](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqSyncConfigExecutor.cs:42) 在同一 DAQ Task 中建立 AI 与 CI；NI 文档要求同一任务通道属于相同 I/O 类型。[NI 官方说明](https://www.ni.com/en/support/documentation/supplemental/15/channel-expansion-explained.html)

[SyncRead](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqSyncReadExecutor.cs:35) 先读 N 个 AI，再读一个编码器标量；波形编码器仅一个点。[CSV](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/DaqFileWriter.cs:60) 将该标量复制到每一行，不能产生 N 个同步编码器样本，也没有实现多编码器结果。

建议：复合资源持有独立 AI/CI Task，按设备支持共享采样时钟/触发；读取一致点数并验证时间轴和样本数。不支持时明确拒绝，不能复制标量伪装同步数据。

验收：仿真数据对齐检查及真实硬件共同脉冲/时钟验证；设备错误码和可用路由仍需现场确认。

### F08．NI DAQ 全局同名任务会相互销毁，缺少序列生命周期归属

证据：**源码确认。** [注册表](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/NiDaqTaskRegistry.cs:13) 是进程级字典，仅按 TaskName 索引；[配置](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqSyncConfigExecutor.cs:34) 自动移除并 Dispose 同名旧任务，未确认旧读取退出，且忽略 Dispose 错误。AI/Encoder 配置有同样模式。

触发：两个序列使用同名任务，或前轮异常退出未执行 Stop。第二次配置可以销毁第一轮资源；任务未注册到上下文资源系统，跳过 Stop 后没有本插件保证的回收路径。

建议：定义明确上下文/连接所有者；默认拒绝重名，替换显式并确认退出。成功注册前的创建失败也必须清理临时任务。

### F09．OPC UA 订阅忽略 StatusCode，可以把坏质量当作条件满足

证据：**源码确认。** [订阅](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Executors/OpcUaSubscribeExecutor.cs:45) 的首次读取、通知回调和二次读取均仅用 `Value?.ToString() ?? ""` 比较。

具体反例：Bad 状态、Value=null 转成空字符串，配置“NotEqual 0”即满足，并返回 Completed。数据存在不等于质量可用，应保留并处理 DataValue 的状态及时间信息。[OPC Foundation DataValue 契约](https://reference.opcfoundation.org/specs/OPC-10000-6/5.2.2.17)

建议：统一质量策略，默认 Bad 不参与成功判定，Uncertain 显式配置；输出类型和值、质量、源时间。不能用坏质量触发设备前置条件。

### F10．DoIP 缺少响应地址关联与事务归属

证据：**错误地址离线回环复现；并发/迟到交错为源码确认。** [DiagRequest](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/DoIP/DoipClient.cs:55) 收到 `0x8001` 只要长度至少 4 就返回 `data[4..]`，不核对 SA/TA，也无完整事务门。

探针：诊断仪 `0E00` 请求 ECU `1000`，伪服务端响应 SA=`9999`/TA=`8888`，仍接受 `50 01`。多个同 Session 请求还可能交错消费响应。

[执行器](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/DoIP/Executors/DoipDiagRequestExecutor.cs:59) 在调用方取消时保留会话；一般协议/流异常也保留。迟到或流对齐未知的字节可影响下一事务。**内部超时分支已关闭会话，不把这一分支误报为未处理。** 每个 ACK 循环重新创建接收期限，也缺少请求总截止时间。

建议：连接级事务串行化、验证地址/请求归属、统一总期限；取消或协议异常后按已消费字节和协议状态决定隔离/关闭。保留诊断，不自动重发有副作用请求。

## P2：其他明确缺陷

### F11．TCP / DoIP 同名注册存在 remove→await→赋值竞态

证据：**源码确认。** [TCP](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/TcpConnectionManager.cs:19)、[DoIP](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/DoIP/DoipConnectionManager.cs:21) 在异步建立连接前删除旧项，完成后直接字典赋值。

两个并发同名 Connect 可互相覆盖并遗失一个未 Dispose 的连接；Close 在 Connect 等待期间返回后，旧 Connect 仍可重新注册。池为进程全局，未挂入上下文资源，异常跳过显式 Close 时易遗留。

建议：按名建立事务/代次，原子注册、明确重名策略和失败清理，关闭与建立协同；定义运行上下文归属。验收覆盖双 Connect、Connect 中 Close、新旧代次交错。

### F12．LIN 关闭忽略 nxStop/nxClear 失败并丢弃句柄

证据：**源码确认。** [Close](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Adapters/NiXnet/NiXnetLinAdapter.cs:101) 不检查返回值，finally 无条件清零句柄并标记未连接。驱动失败时丢失实际资源和重试依据。

建议：检查每项返回值，区分请求停止、任务退出、驱动停止和句柄释放；只清除已确认释放的句柄。验收注入各阶段驱动失败。

### F13．Kvaser 部分打开失败泄漏通道

证据：**源码确认。** [Open](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Adapters/Kvaser/KvaserAdapter.cs:50) 获得句柄后，SetBusParams/SetBusParamsFd/BusOn 可能抛错；[Close](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Adapters/Kvaser/KvaserAdapter.cs:83) 因 `_isConnected=false` 直接返回，外层失败清理也释放不了已打开句柄。

建议：按句柄有效性清理，不按最终连接成功标志决定；部分初始化建立逆序清理和可诊断失败状态。原生打开/关闭错误码需 SDK fake 和设备验证。

### F14．VISA WaitOPC 收到任意字符串都会报告操作完成

证据：**源码确认。** [WaitOPC](/D:/project/xTestPlatform_Plugin/VISA/VisaPlugin/Executors/VisaWaitOpcExecutor.cs:55) 不验证 `*OPC?` 回包；`0`、空行或另一条命令回复也能 Completed。仪器文档定义操作完成返回 1。[Keysight *OPC?](https://helpfiles.keysight.com/csg/e5063a/programming/command_reference/ieee/opc.htm)

建议：严格解析预期的完成值，并解决 F01 的事务残留问题；不能用 contains("1") 代替解析。

### F15．VISA WaitOPC 的超时属性异常可泄漏 I/O 锁

证据：**源码确认。** [加锁后属性访问](/D:/project/xTestPlatform_Plugin/VISA/VisaPlugin/Executors/VisaWaitOpcExecutor.cs:43) 位于 try/finally 之前；finally 恢复 Timeout 也在 `gate.Release()` 之前，属性 getter/setter 抛错都会使后续步骤一直等锁。

建议：获得锁后立即进入保证释放的 finally；恢复设置的失败不得阻止解锁，并保留原操作异常及清理异常。验收用抛出属性异常的 fake Session。

### F16．串口终止符模式吞并后续帧

证据：**源码确认。** [Read](/D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin/Helpers/SerialPortHelper.cs:146) 读取整个块，只检查是否包含终止符，直接返回整个数组，无余量缓冲。

例如一次 OS Read 返回 `A\nB\n`，第一步消费两帧，第二步没有 B。建议按首个终止符切帧，连接级保存残留；覆盖多帧、半帧及跨块多字节终止符，并与 F01 的事务门一起处理。

### F17．Modbus 保持寄存器 UI 保存成只读离散输入

证据：**源码确认，未运行实际页面。** [下拉](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin.UI/Views/ModbusWriteEditorView.xaml:40) 仅两个选项，Holding 在索引 1；[VM](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin.UI/ViewModels/ModbusWriteViewModel.cs:49) 直接转换枚举，而 [枚举](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin/Models/ModbusEnums.cs:24) 的 1 是 DiscreteInput，HoldingRegister 是 2。

选择 Holding 后执行器按只读类型拒绝；默认 Holding=2 也不在下拉索引范围。建议显式 SelectedValue 映射，**不要更改已序列化枚举数值顺序**。

### F18．Modbus 非法线圈值被静默解释为 false

证据：**源码确认。** [单写](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin/Executors/ModbusWriteExecutor.cs:61) 与批量写仅将 `1`/`true` 视为 true，其余一律 false，包括 `2`、`tru`，可以完成实际 OFF 输出并报告成功。

建议：严格接受 0/1/false/true；批量先验证全部条目再开始硬件写入。若业务要求其他表示，应显式定义而非 fallback false。

### F19．OPC UA 后台采集丢失质量/源时间与故障健康状态

证据：**源码确认。** [采集](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Helpers/OpcUaDataAcqTask.cs:64) 丢弃 StatusCode/SourceTimestamp，异常只跳过；`IsRunning` 仅看 CTS。[读取](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Executors/OpcUaDataAcqReadExecutor.cs:33) 只检查溢出，即使持续通信失败，也能返回 Completed/0 samples，或输出此前缓存而不携带健康状态。

Dispose 仅取消不等待 worker；Disconnect 没有协调绑定到该 Session 的采集任务。允许跳过一次采集失败是现有语义，不将它单独认定为缺陷；问题是没有 LastError/最近成功/连续失败/真实任务退出状态，调用者无法区分健康空队列与后台失败。

建议：每点保留质量和源时间，任务公开健康及退出结果；断开前确认相关 worker 退出。波形不能把网络轮询延迟和丢样抹成固定采样周期。重连仍由用户序列控制，不新增自治重连。

### F20．OPC UA 订阅创建失败不进入清理范围

证据：**源码确认。** [AddSubscription/CreateAsync](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Executors/OpcUaSubscribeExecutor.cs:84) 在清理 try/finally 之前；创建取消/抛错可留下已附着的 Subscription。finally 的 DeleteAsync 抛错也会跳过 RemoveSubscription 并覆盖原错误。

建议：从资源创建开始统一托管；清理使用独立有界期限，保证本地解除关联并报告服务端删除失败，保留主异常。验收创建中失败、调用方取消和删除失败。

### F21．OPC UA 单点读写的步骤 TimeoutMs 没有生效

证据：**源码确认。** [ReadSetting](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Models/OpcUaReadSetting.cs:21) 定义超时，但 [Read](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Executors/OpcUaReadExecutor.cs:42)、[Write](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Executors/OpcUaWriteExecutor.cs:73) 直接传调用方令牌，没有应用步骤期限，实际依赖连接 OperationTimeout。

建议：明确连接超时/会话期限/单步骤请求期限，不并发修改共享 Session 全局超时来模拟单请求期限。批量步骤的 Description 已注明该参数保留，本报告不把它们说成隐藏实现承诺。

### F22．OPC UA 采集 UI 的节点修改不保存、删除按钮无效

证据：**源码确认，未运行实际页面。** [VM](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin.UI/ViewModels/OpcUaDataAcqStartViewModel.cs:33) 没有订阅 item 变化；[DataGrid](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin.UI/Views/OpcUaDataAcqStartEditorView.xaml:70) 未调用 `NotifyItemChanged()`。单独编辑已有 NodeId/ColumnName 不触发序列化，除非另一个操作偶然 QueueSave。

[Remove](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin.UI/Views/OpcUaDataAcqStartEditorView.xaml.cs:37) 要求按钮 DataContext 为 item，但按钮在 DataGrid 外，继承的是整个 VM，正常页面结构下无法删除。

建议：绑定 SelectedItem 删除；可靠的 item 变化/提交事件保存，包含关闭和刷新时 Flush。页面回读序列化内容验证，而不仅检查显示值。

### F23．LIN 版本/ChecksumType 配置没有落实到 NI 驱动

证据：**源码确认未使用字段；实际硬件行为未验证。** [NI Open](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Adapters/NiXnet/NiXnetLinAdapter.cs:40) 未使用 `config.LinVersion`；[组帧](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Adapters/NiXnet/NiXnetLinAdapter.cs:228) 未使用 `frame.ChecksumType`。

建议：按当前 NI SDK 的有效配置接口实现版本/校验，或启动前明确拒绝不支持的选择；不能仅 UI 保存却表示已应用。不要猜测 NI 默认校验等同用户选项。

### F24．NI DAQ 导出格式选项被忽略

证据：**源码确认。** AIRead/SyncRead UI 保存 ExportFormat，但 [AI 导出](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqAiReadExecutor.cs:67)、[Sync 导出](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqSyncReadExecutor.cs:80) 只判断 SaveToFile，固定输出 CSV。

建议：真正实现 CSV/TDMS/变量等已展示语义，或禁用并明确拒绝未实现选项；增加选择值与文件类型/内容的回读测试。

### F25．NI TaskStop 先删除登记，Stop 失败后资源丢失

证据：**源码确认。** [TaskStop](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqTaskStopExecutor.cs:29) 先 Remove，再 Stop，再 Dispose；Stop 抛错时后两者未完成、注册项已消失，按名重试也找不到资源。

建议：保留所有者与引用直至实际退出确认；失败保持隔离/可诊断，安全释放顺序与 F01/F08 一起设计。

### F26．HTTP 非法 JSON 路径悄悄返回父节点

证据：**离线复现。** [Tokenize](/D:/project/xTestPlatform_Plugin/Http/HttpPlugin/Helpers/JsonPathHelper.cs:62) 先 yield 属性，再在非法索引时 yield break；`items[oops]` 返回 `[10,20]` 而非报错/null。[提取执行器](/D:/project/xTestPlatform_Plugin/Http/HttpPlugin/Executors/HttpJsonExtractExecutor.cs:46) 因非 null，不触发 `FailOnMissingPath`，仍写变量并完成。

建议：先验证完整路径语法，非法语法与未命中分开报错；禁止部分解析成功。一次解析 JSON 后复用节点，批量先校验/提取再提交输出，避免失败前更新一半变量。

### F27．HTTP 客户端创建失败泄漏临时资源，重名替换非原子

证据：**源码确认。** [BuildClient](/D:/project/xTestPlatform_Plugin/Http/HttpPlugin/Helpers/HttpHelper.cs:95) 只保护 `new HttpClient`；之后 BaseAddress/Authorization 异常不 Dispose client，证书加载前后的 handler 失败路径也未统一托管。[Create](/D:/project/xTestPlatform_Plugin/Http/HttpPlugin/Executors/HttpClientCreateExecutor.cs:39) Contains→Remove→多个 await→Set，不是原子注册；默认拒绝重名也挡不住两个并行创建同时通过检查。默认头表达式失败时也未释放已创建 client。

替换先移除旧客户端，再验证新凭据/证书，失败会损失原连接。具体 Resources.Set 覆盖是否释放旧值依赖 SDK，不据此额外断言一定泄漏。

建议：先构建并完整验证临时资源，原子注册/交换，提交失败 Dispose 新资源；明确运行中的请求如何退出与旧资源何时释放。

### F28．XCP Connect 把输出变量写入了资源注册表

证据：**源码确认。** [Connect](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/XCP/Executors/XcpConnectExecutor.cs:28) 的 ResourceVariable 是变量路径，却调用 `context.Resources.Set`；填写 `Locals.xxx` 不更新对应变量，仍返回 Completed。

建议：使用平台变量接口并验证类型；资源键与变量路径不能混用。

### F29．ISO-TP 重组长度没有业务上限，可请求巨额内存

证据：**源码确认，故意未运行大内存实验。** [扩展长度](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/UDS/IsoTp/IsoTpTransport.cs:217) 允许至 `int.MaxValue`，随后立即 `new byte[totalLen]`。首帧 `10 00 7F FF FF FF` 足以请求约 2 GB 分配，不需要先提供完整数据。

建议：配置明确最大重组长度，分配前校验并按协议拒绝超限，测试边界使用小上限而非实际大分配。

### F30．SOME/IP 长度转 int 溢出，TryDecode 会抛异常

证据：**离线复现。** [TryDecode](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/SomeIP/SomeIpMessage.cs:40) 将 uint 长度转 int 后运算。16 字节头且 length=`FFFFFFFF` 绕过长度检查，最终 AsSpan 抛 `ArgumentOutOfRangeException`，违反“非法返回 null”的契约。

建议：用无符号/受检算术比较长度及缓冲边界，再转 int；检查协议版本。无效 UDP 数据应按明确策略忽略/计数，不应意外中断正常请求。

## 条件风险与优化，不冒充已复现缺陷

### R01．多插件 UI 防抖保存缺少稳定对象与切换 Flush

CAN/LIN/Modbus/OPC UA/VISA 等 VM 常见 200 ms 后台保存：任务在延迟后读取可变 `_step/_setting`，Attach/Load/Refresh 不先 Flush/取消并等待。快速刷新/复用编辑器可丢失旧修改；后台序列化 ObservableCollection 还可能与 UI 编辑并发，非取消异常也无人观察。

关键例：[ModbusWriteViewModel](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin.UI/ViewModels/ModbusWriteViewModel.cs:39)、[OpcUaDataAcqStartViewModel](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin.UI/ViewModels/OpcUaDataAcqStartViewModel.cs:21)。需宿主实际刷新时序/页面运行验证，不能以编译成功排除。可参考当前 CiA402 同步 Flush/Dispatcher 保存方式，但应在受影响页面逐项验证而非机械复制。

### R02．HTTP 认证边界与日志脱敏不完整

- [默认 Authorization](/D:/project/xTestPlatform_Plugin/Http/HttpPlugin/Helpers/HttpHelper.cs:124) 配合 Request 的任意绝对 URL，在 Path 由非可信/动态外部值驱动且可跨域时，可能把 Basic/Bearer 发给非预期主机。应定义允许 origin，或明确“跨域且携带凭据”为需要显式选择的能力；这是条件风险，不认定所有绝对 URL 都错误。
- [Create 日志/结果](/D:/project/xTestPlatform_Plugin/Http/HttpPlugin/Executors/HttpClientCreateExecutor.cs:80) 输出原始 BaseUrl；若带 userinfo/查询 token，会绕过请求诊断函数的脱敏。建议所有日志/错误/结果统一脱敏。
- HTTP/OPC UA 的跳过证书校验是显式非默认选项，不把它们误报成默认不安全；建议 UI 明确告警和运行日志显示安全模式。

### 其他值得优化的点

1. **统一资源模式，不统一危险回退。** 让每个插件资源拥有真实 I/O、任务、故障与退出状态；可共享小型生命周期工具，但不把不同 SDK 的 Stop/Abort/Close 语义强行合并。CiA402 不应接管或擅自关闭父 CAN。
2. **错误日志结构化。** 连接/任务名、代次、操作、期限来源、调用方取消、底层是否退出、原始错误/清理错误分别记录。不能把驱动故障吞成 Completed 或无健康状态的空结果。
3. **采集记录保留实际时间轴。** OPC UA 网络轮询不是硬件等间隔采样；NI 波形读取完成后的 DateTime.Now 也不是采集起点。区分主机观察、设备源时间与未知时间。
4. **严格输入校验。** Modbus 32 位转换输入奇数寄存器会丢尾项，SlaveAddress 整数直接转 byte 可回绕；应在任何输出之前验证完整配置。保留已序列化枚举与字段的兼容值。
5. **文件完整性。** NI CSV 同秒轮转名称冲突、同文件并发追加缺少协调；OPC CSV 缺少字段引号/换行转义。需要针对输出格式回读测试，避免仅以文件存在为成功。
6. **HTTP 连接复用可配置。** 当前强制 ConnectionClose 用于兼容不发送长度的设备；高频标准服务会付出重连成本。保留兼容模式，按目标能力选择，不无条件删除此设置。
7. **提取效率。** HTTP 多字段 JSON 每行重新解析整段文本；可共享一次解析结果。避免为微小性能收益先改设备安全时序。
8. **CI 缺少测试门禁。** [release.yml](/D:/project/xTestPlatform_Plugin/.github/workflows/release.yml:60) 先推 tag、后构建，没有执行 dotnet test；产物验证主要列目录。CAN 测试尚未列入 slnx。建议 PR 构建/测试先通过再建立发布 tag，校验插件版本配对、主 DLL/UI/Native 布局与解压加载；本轮不执行真实发布验证。

## 验证记录

### 实际执行

| 检查 | 结果 | 证据/限制 |
| --- | --- | --- |
| 10 类插件项目/源码库存与关键调用链阅读 | 完成 | 核心、资源、协议、UI 映射；不是逐行/全硬件审计 |
| 独立只读审查 | 完成 | CAN/CiA402/LIN、Modbus/NiDaq/SerialPort、OPC UA/VISA 三组；追加独立复核 Ethernet/HTTP，主审核实重点位置 |
| ECC C# / 异常传播专项 / HTTP 信任边界检查 | 完成 | 发现对应上文，未实施修复；不是仅列方法名称 |
| 全解决方案 Release Rebuild | 通过，0 错误、2 警告 | 20 个核心/UI 项目和 slnx 中 CiA402 测试；见下方命令 |
| CAN 当前假设备/契约测试 | 76/76 通过，0 跳过 | `artifacts/plugin-audit/test-results/can.trx` |
| CiA402 当前假设备/契约测试 | 204/204 通过，0 跳过 | `artifacts/plugin-audit/test-results/cia402.trx` |
| 本轮诊断探针 | 13/13 观察到预期缺陷行为 | “REPRODUCED”是复现缺陷，不是功能验收通过 |
| 本轮文档/工具 UTF-8、路径和差异检查 | 通过 | 7 个本轮文件严格 UTF-8 检查、64 个本地链接/行号存在，git diff --check 退出码 0；不转换/覆盖已有源文件编码 |
| 真实设备/驱动、WPF 页面实际交互、部署加载 | 未执行 | 没有启动平台、连接设备或重放真实输出；构建不替代这些验证 |
| 全仓库覆盖率/依赖漏洞扫描 | 未执行 | 不宣称覆盖率或无漏洞；还原明确关闭 NuGetAudit，避免把 restore 当作漏洞检查 |

Rebuild 的两条已有警告：`CAN/CanPlugin/CANopen/CanopenExecutorHelper.cs:100`、`Modbus/ModbusPlugin/Executors/ModbusReadExecutor.cs:86` 的 CS8601。本轮未修改这两处。先前增量构建显示 0 警告不代表干净重编无警告，以 Rebuild 为准。

最初 no-restore 构建因缺少资产失败；使用任务本地 NuGet 配置和本机缓存还原后，OPC Core `1.5.374.158` 仍缺失，再从官方 NuGet 恢复既定依赖，最终 Rebuild 成功。未更改项目依赖版本或用户全局 NuGet 配置。

### 可重复命令

在仓库根目录，依赖已还原后：

```text
dotnet build xTestPlatform-PluginDev.slnx -t:Rebuild -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-audit/build -v minimal -m:1
dotnet test CAN/CanPlugin.Tests/CanPlugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-audit/test-can --logger trx;LogFileName=can.trx --results-directory D:/project/xTestPlatform_Plugin/artifacts/plugin-audit/test-results -v minimal -m:1
dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj -c Release --no-build --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-audit/build --logger trx;LogFileName=cia402.trx --results-directory D:/project/xTestPlatform_Plugin/artifacts/plugin-audit/test-results -v minimal
dotnet run --project artifacts/plugin-audit/Probes/Probes.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-audit/probe-build
```

探针链接当前 HTTP/Ethernet/Modbus/DAQ 辅助源码并引用当前 CAN/VISA 项目，不是另写一套算法代替原实现。网络仅使用 127.0.0.1 临时端口；假 CAN 不打开任何适配器；受控阻塞委托均在观察后释放。

探针最终结果摘要：

```text
ISO_TP_FC_31 / ISO_TP_FC_32: CF count=2 after Wait/Overflow
UDS_RESPONSE_62 / 62F190 / 7699: IsPositive=True, ProtocolError=null
TPDO_OLD_FRAME_AGE: ingress age=10009.9ms, QueueAge=19.2ms
HTTP_INVALID_JSON_PATH: items[oops] returned [10,20]
FLASH_ADDRESS_TRUNCATION: EncodeValue(0x02000000,1)=00
SOMEIP_LENGTH_OVERFLOW: ArgumentOutOfRangeException
DOIP_WRONG_ADDRESS: accepted SA=9999/TA=8888
VISA_WAIT_CANCELLED_NATIVE_RUNNING: wrapper completed=True, delegate exited=False
NIDAQ_WAIT_CANCELLED_NATIVE_RUNNING: wrapper completed=True, delegate exited=False
MODBUS_TIMEOUT_OPERATION_RUNNING: original operation completed=False
SUMMARY reproduced=13, not_reproduced=0
```

### 复核与排除的结论

- CAN 1.0.33 的运动代次停止判定、监测 SYNC 继续、ZLG 混合回显/时钟锁等已有修复，不重复列为未修缺陷；现有 76 项测试继续通过。仍须真实 NI/ZLG 总线顺序、周期和停止验证。
- CiA402 当前绑定不再因为 Bind 步骤调用方令牌结束就主动终止轴；被动 TPDO 与未退出 SDO/native 仍应区别处理。这里的新问题 F03 是共享接收帧龄，不是主张取消隔离或旁路父 CAN。
- 当前 OPC UA BatchRead 已检查 Bad 状态，不沿用历史“批量读取忽略状态”的旧结论。不同连接名称关闭一个 Session 就会主动关闭全部的说法，当前客户端代码没有证据；本报告没有把它当已确认根因。
- SOME/IP Subscribe 描述就是本地通知监听，不把“没有自动 SD 订阅握手”当实现承诺违约。
- 未报告默认 TLS 校验关闭或 XML 默认外部实体漏洞；没有相应证据。
- 尚无修复提交，因此不存在本轮“问题修复后复测通过”的声明；上面的测试是当前基线和缺陷复现证据。不能因现有测试通过而忽略已复现问题。

## 建议实施顺序与回退约束

1. **生命周期优先**：F01/F02/F08/F11/F12/F13/F15/F25，统一“真实退出前不复用/释放”的边界，按插件分批落地，不重写全部协议。
2. **动作与判定正确性**：F03–F07/F09/F10/F14/F18，先加失败用例再实现。Flash 预检应位于任何擦除前；反馈未知不能强制有效；ACK 不等于物理停稳。
3. **配置/UI/结果一致性**：F17/F21–F24/F26/F28 与 R01。保留序列化字段、枚举值及旧序列兼容；未实现功能明确拒绝。
4. **资源上限/信任边界/性能与 CI**：F19/F20/F27/F29/F30、R02 和上述优化项。

每批交付包含故障注入、相关 core/UI 构建、序列化回读和退出确认测试；设备相关最后安排单独硬件资格验证。回退保留原插件整目录和测试序列，不恢复“未确认退出也继续”的危险 fallback，不通过关闭确认、改计数或忽略 Error 来让测试变绿。
