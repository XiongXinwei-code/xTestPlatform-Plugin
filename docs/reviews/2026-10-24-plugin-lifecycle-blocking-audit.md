# 全插件生命周期、立即重跑与阻塞审查

## 结论

当前仍有需要修复的缺口，不能认定“全部插件中断后可立即重跑、所有步骤都有完整的不阻塞边界”已经完成。本轮确认 **5 项 P1、7 项 P2**，另列条件性风险与未验证项。这里的优先级是软件修复顺序，不表示已经在真实设备上发生事故。

最需要优先处理的不是统一增加一个固定超时，而是：

1. 尚未提交的旧操作，在超时或关闭后仍可能开始执行。
2. 部分 CAN/UDS/XCP 同步 I/O、DAQ 驱动枚举和数据导出，绕过调用方的软超时/取消等待边界。
3. 已能安全释放的资源，被历史错误阻止清理，或者让首次重跑失败。
4. 部分设置有字段但未接入执行，另一些等待根本没有可配置期限。

本轮是 **L3 只读审查**，没有修改生产插件、版本、实际序列或交付包，没有提交/推送，也没有操作设备。已新增审查文档与离线诊断探针；下列“复现成功”表示发现得到验证，不是修复通过。

## 基线与证据边界

- 审查对象：当前工作区 10 个插件、20 个 core/UI 项目、118 个步骤。HEAD=`5351a0a9d66594455b292fe65308303488a07537`，包含大量已有未提交修改，**不是对远端 master 或用户现场实际加载 DLL 的认证**。
- [源码基线清单](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/source-baseline.json)：1080 个源码/项目/页面文件（含测试与工具），聚合 SHA-256=`0b79cce224b3182dca1fe5cbb00cba2fe30ec0963b1d37fe18683067eff268f7`。只读范围按步骤入口到资源/协议/后台任务的调用链覆盖，不声称穷尽所有输入、第三方 ABI、图标资源和全部分支组合。
- [交付来源核对](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/delivery-source-receipt.json)：21 个插件程序集（含 CiA402 私有 CAN 副本）及 9 个测试程序集，PE/PDB 配对、1199 条本地非生成 C# 文档哈希均与当前源码匹配；不加载原生驱动。不以此代替 XAML 运行或硬件验证。
- 本轮 CAN 探针实际使用 `CAN.StepPlugin.dll 1.0.34.0`，MVID=`c9e74f1b-7f6a-4f16-bb39-fcd9fc04f6a8`，SHA-256=`5e458ee099f22599e19425a4db3938b4fc7733b4de410561932625f824570f11`。其他程序集身份见来源回执。
- 判据来自开发手册 [阻塞 I/O、配置期限及配置四处闭环](/D:/project/xTestPlatform_Plugin/xTestPlatform_StepPlugin_Development_Guide.md:138)、[执行资源回收](/D:/project/xTestPlatform_Plugin/xTestPlatform_StepPlugin_Development_Guide.md:950)、[异常/取消语义](/D:/project/xTestPlatform_Plugin/xTestPlatform_StepPlugin_Development_Guide.md:2147)。审查没有把旧报告中的行号当作本轮证据。
- 证据分类：**托管复现**=当前源码或已核对程序集配合可控替身；**源码确认**=完整调用与配置数据流；**条件风险**=缺口存在，但现场 SDK、磁盘、DNS 或宿主线程触发尚未验证。

## P1：优先修复

### LCA-01．共享 I/O 门允许超时后尚未提交的工作者迟到执行

位置：[PendingIoGate](/D:/project/xTestPlatform_Plugin/Shared/Runtime/PendingIoGate.cs:43)，实际工作者在第 50–54 行，限时等待在第 59–71 行。

调度前检查了剩余时间，但 `Task.Run` 工作者内部只检查外部令牌，没有重新检查单调截止时间及本次提交许可。内部超时不会取消这个外部令牌。因此交错可以是：排队 → 等待者超时并隔离 → Close 开始 → 原委托才运行。

**托管复现三次一致**：`CALLER_TIMEOUT submitted=0 pending=True`，放开入口屏障并关闭后 `AFTER_CLOSE submitted=1 closed=True`。这里不是无法撤回已发报文，而是本来还没提交的操作在超时后开始。

影响与排除范围：

- [Serial Write/Query](/D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin/Helpers/SerialConnectionResource.cs:62) 没有提交前单调期限防护；Write 不检查生命周期，Query 写前只检查外部令牌。
- [Modbus 正常事务](/D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin/Helpers/ModbusConnectionResource.cs:68) 直接传入工厂，内部超时后仍可能迟到开始；TCP 关闭后的 transport 另有停止令牌，不声称其 Close 后必定继续发送。
- [LIN Write/Wakeup/Sleep](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Runtime/LinConnectionResource.cs:38) 有部分关闭/外部令牌检查，但未检查原始单调期限；超时且未关闭时仍有窗口。
- DAQ 的 `EnsureOperationActive`、VISA 的 `ThrowIfSubmissionStopped` 有额外单调预算防护；OPC UA 不走该门，不能笼统说这些模块也都会迟到写。

修复与验收：在真正执行委托前检查原始令牌、单调期限及提交代次/冻结状态；仍保留真实任务直到退出。覆盖“排队未提交时超时”“超时后 Close”“已进入 native 后取消”三种不同情况。详见 [独立复现记录](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/audit-shared-net/PROBE_RESULTS.md)。

### LCA-02．CAN 原始协议路径仍有未受软预算保护的同步 I/O，XCP 先发送后检查取消

位置：[CAN_Read](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Executors/CanReadExecutor.cs:38)、[连接同步读写](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Runtime/CanConnectionResource.cs:230)、[ISO-TP](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/UDS/IsoTp/IsoTpTransport.cs:51)、[UDS 请求](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/UDS/UdsClient.cs:121)、[XCP 发送](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/XCP/XcpClient.cs:125)。

- `CAN_Read` 直接进入同步 `adapter.Read`；UDS 单帧/首帧/CF/FC 仍同步 `adapter.Write`，接收也同步进入原始适配器。`CanConnectionResource.WriteAsync` 的软等待保护没有被这些协议路径采用。
- UDS 的 `ResponseTimeoutMs` 从响应阶段起作用，不是发送阶段的保护；连接级 UDS 事务门也只有外部取消，没有排队预算。不能把响应超时字段当作整条同步调用已经限时。
- XCP `SendReceiveAsync` 先 `Write`，到 `WaitResponseAsync` 才检查外部令牌。已经取消的调用仍可能提交 `SET_MTA` 等命令；同一实例的复合命令/不同 XCP 客户端也缺少完整事务归属门，不能靠各自读响应保证关联。

**当前交付 DLL + 假适配器复现**：UDS 发送中令牌取消并等待 160 ms 后，异步调用连 Task 都没有返回；XCP 传入已取消令牌仍发送了一帧 `F6 ...` 再抛取消。回执：[CAN/LIN/CiA402 探针](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/audit-can-cia-lin/receipt.json)。这不证明真实 NI/ZLG 一定会挂住，但证明插件没有独立于驱动配合的等待边界。

影响：原始驱动阻塞时，调用线程/序列停止可能一直等；新一轮因旧实际 I/O 仍在而正确拒绝占用，表面上表现为“重跑通道被占用”。仅改 Close 或删除物理占用表不能修复这个入口。

修复与验收：把原始读写和协议完整事务纳入有配置期限的 owner 路径；每次副作用提交前检查取消/单调期限；已提交 native 仍追踪到退出，未知事务保持隔离。覆盖预取消零发送、发送/接收不配合取消、排队期限、迟到响应和复合 XCP 命令不能交错。不自动重发有副作用命令。

### LCA-03．全部 DAQ 步骤在软超时之前同步触发原生设备枚举

位置：[NiDriverCheck](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/NiDriverCheck.cs:13)、[AI 读取](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqAiReadExecutor.cs:24)、[TaskStop](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqTaskStopExecutor.cs:23)。10 个 DAQ 执行器均调用 `EnsureDriver`。

`DaqSystem.Local.Devices` 在调用线程同步执行，尚未进入 `DaqTaskResource` 的软预算或传入取消令牌。如果驱动加载/首次枚举阻塞，配置期限和 Stop 步骤也尚未生效。托管 getter 替身确认这条入口没有截止边界；**没有实际加载 DAQmx，未宣称真实 NI 枚举已复现卡住**。

此外，任何枚举异常均被吞掉并永久缓存 `_available=false`，后续一律表述为“未安装驱动”；临时枚举失败不能在同一进程恢复，原始错误也丢失。

修复与验收：初始化检查同样由 owner 追踪并软限时；区分依赖缺失与运行时枚举失败，保留原错误，避免永久毒化缓存。已持有资源的收尾不应先依赖一次新的设备枚举。用阻塞/临时失败 getter 覆盖取消、重试、并发首次检查和 TaskStop。

### LCA-04．DAQ/OPC 数据导出与消费回调可以绕过步骤退出边界

位置：[DAQ AI 导出](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Executors/NiDaqAiReadExecutor.cs:35)、[DAQ 文件锁/Flush](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/DaqFileWriter.cs:20)、[OPC 采集读取](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Executors/OpcUaDataAcqReadExecutor.cs:54)、[OPC 消费门](/D:/project/xTestPlatform_Plugin/OpcUa/OpcUaPlugin/Helpers/OpcUaDataAcqTask.cs:109)。

- DAQ AI/SyncRead 的设备读取受 `ReadTimeoutMs` 保护，之后却同步进入 CSV 的无界 Monitor、文件读写和 `Flush(true)`，没有取消/导出等待预算。慢文件系统或前一写者卡住时，读取结束不等于步骤可退出。
- OPC DataAcqRead 没有可配置总期限；`ConsumeAsync` 只在进消费者门时检查调用方令牌，进入后直接等回调。CSV 虽放在 `Task.Run`，却无取消等待；写出卡住会持有消费者门，后续 Stop 的消费阶段也受影响。后台不能因前台停止等待而随意消费/丢弃未提交的数据。

**托管复现**：持有生产 DAQ `FileGates` 时 AppendCsv 持续等锁；OPC 回调已经进入后取消调用方，120 ms 后仍未返回。CSV 真正磁盘挂起没有实测，已确认的是生产代码的等待边界缺口。

修复与验收：导出/消费也要有可配置预算与真实任务/代次归属；取消可结束调用方等待，但实际文件事务/消费者门必须保留到退出，阻止晚到任务向新运行发布结果。覆盖慢盘/锁竞争、超时后晚完成、导出失败不丢 FIFO、Stop 与导出并发，不以提前解锁制造重复消费。

### LCA-05．已故障的监测 SYNC 服务，单独停服可能不能释放可安全释放的资源

位置：[StopTargetsAsync](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Runtime/CanopenCyclicService.cs:412)、[CloseAsync](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Runtime/CanopenCyclicService.cs:477)、[StopServiceAsync](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Runtime/CanConnectionResource.cs:196)。

监测 worker 已因错误退出后，`StopTargetsAsync:451` 重新抛出捕获的历史错误；`CloseAsync:480` 因而不再执行后续 RequestStop、ConfirmExit、订阅释放和 closed 标记；连接在 StopService 成功后才移除服务。

**本轮再次复现**：纯托管确认型 fake，监测周期 10 ms、fake 发送 40 ms，进入周期超限 Faulted；`HostExited=true`、submitted=confirmed=1、`OwnedTxQuiescent=true`，仍 StopService 失败、服务保留。停止没有新增发送。整连接关闭使用另一条 CloseForConnection 路径，能实际关闭并释放物理键，同时保留历史错误。见 [本轮回执](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/can-service-fresh-receipt.json)。

不是“所有 worker 故障都会永远清理失败”：不启用监测的输入过期反例可以单独停服，已一并验证。正常运动停止的代次结果检查也已经存在，不再把旧的全局计数问题当作当前未修问题。

修复与验收：历史通信错误与实际资源释放结果分开；退出/发送收尾能确认时仍完成安全清理并提供错误诊断；PartialCycle、发送未知、旧代次保护不能删除，不能靠忽略 Error 或关确认规避。覆盖“已故障且全部确认”“未确认/部分周期”“正常停后监测下一拍仍在确认”三种情况。

## P2：配置、首次恢复与异常契约

### LCA-06．旧关闭已释放但抛历史错误时，首次立即重跑仍失败

位置：[CAN 等待旧 owner](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Runtime/CanConnectionResource.cs:63)、[共享退役等待](/D:/project/xTestPlatform_Plugin/Shared/Runtime/RetiringResources.cs:79)、[LIN 已释放后的历史错误](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Runtime/LinConnectionResource.cs:110)。

等待旧 Close 的 catch 没有把“已确认释放、仅有历史错误”与“仍未释放”分开。Shared 甚至先移除已释放 entry，再抛出“未确认释放的所有者仍保留”的消息。CAN 原生 NI/ZLG 支持句柄已释放后继续报告历史 RX 错误，故不是只有假类型才会出现的状态。

**托管复现**：首个等待重开收到 Error，此时旧 owner `IsClosed=true`、活动 I/O=0；第二次等待成功，同物理键新 owner 可建立。[独立探针/回执](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/restart-historical-receipt.json)。Shared 通用行为也已复现，具体消费者仅在其 Close 允许“已释放带错误”时适用，例如 LIN；不能推断所有关闭失败都可放行。

建议：原关闭结果继续保留历史 Error，但新运行在**再次确认该精确旧 owner 已释放**后继续；未释放仍隔离，按配置总预算等待。测试必须覆盖等待期间释放后报错，以及旧回调不能删除/关闭新代次。

### LCA-07．LIN_Sleep 忽略配置期限，驱动内取消后仍可能 Completed

位置：[LinSleepExecutor](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Executors/LinSleepExecutor.cs:37)、[LinSleepSetting](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Models/LinSleepSetting.cs:9)、[默认同步 Sleep](/D:/project/xTestPlatform_Plugin/LIN/LinPlugin/Runtime/LinConnectionResource.cs:58)。

执行器没有读取 `TimeoutMs`，调用 `adapter.Sleep(bool)`，进入固定 3000 ms/default token 的同步包装。**实际步骤 + fake 复现**：配置 20 ms，在 native 内取消，160 ms 后仍未结束；放开 fake 且 `PostSleepDelayMs=0` 后结果为 Completed，而非响应取消。

建议：接入真实配置和原始令牌，调用入口本身不能同步占用宿主线程；返回前检查取消。仍跟踪已提交原生 Sleep 到退出，不把取消解释成设备没有睡眠。补实际执行器测试，而不仅是 Setting 默认值/序列化测试。

### LCA-08．DoIP 诊断步骤的 TimeoutMs 完全没有用于请求

位置：[DoipDiagRequestExecutor](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/DoIP/Executors/DoipDiagRequestExecutor.cs:18)、[实际事务期限](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/DoIP/DoipClient.cs:163)。

模型、页面、VM、校验都有请求超时，Executor 调用却只传 target/data/token；client 使用连接构造时 `_timeoutMs`。修改诊断步骤为 100 ms 或 30 s 均不改变其实际预算。另有诊断误导：事务门排队超时且尚未进入时 client 并未关闭实例，但执行器对所有超时均写“会话已关闭”（该附属分支为源码确认，未单独动态复现）。

建议：每个请求的配置总期限覆盖排队、ACK、完整响应；保留具体实例隔离和实际状态诊断。测试连接 5 s、请求 50 ms/10 s 两组，确保两者不串用；不要为使错误文字成立而关闭有效会话。

### LCA-09．SOME/IP UDP 同步域名解析不受取消和响应期限保护

位置：[Request UDP](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/SomeIP/Executors/SomeIpRequestExecutor.cs:95)、[FireAndForget UDP](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/SomeIP/Executors/SomeIpFireAndForgetExecutor.cs:46)。

两处同步 `UdpClient.Connect(string,int)`；当前响应期限不覆盖 Connect/Send，这个响应计时语义本身不构成故障，问题是同步域名入口没有可取消/总预算边界。已读取 [.NET 8 官方实现](https://raw.githubusercontent.com/dotnet/runtime/v8.0.0/src/libraries/System.Net.Sockets/src/System/Net/Sockets/UDPClient.cs)，该重载同步调用 `Dns.GetHostAddresses`。RemoteHost 可配置域名，慢解析期间令牌无法打断该同步入口。

建议：用预算内可取消的异步解析与发送，或明确只接受 IP 并验证。未注入真实 DNS 故障；只能确认占用当前调用线程，**没有证据说该线程必定是平台 UI 线程**。

### LCA-10．SOME/IP FireAndForget 和 UdpSend 缺少可配置发送期限

位置：[FAF TCP/UDP](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/SomeIP/Executors/SomeIpFireAndForgetExecutor.cs:40)、[UdpSend](/D:/project/xTestPlatform_Plugin/Ethernet/EthernetPlugin/Executors/UdpSendExecutor.cs:35)。模型、VM、页面没有 Timeout/SendTimeout 字段。

FAF TCP 的连接/发送只用外部取消令牌，无应答/背压时需依赖 OS 或人工中止；UDP 通常快速，但同样没有配置上界。这些异步 Socket 调用确实接收外部令牌，using 也存在，**不把缺少内部期限等同于中断必然卡死**。

建议：增加兼容默认的发送总预算，覆盖解析、连接、提交；模型、描述、VM/页面、ValidateWithContext 同步。用连接不应答/发送背压的模拟链路验证内部超时 Error 与外部取消 Aborted。

### LCA-11．部分 CAN 停止/简单发送仍使用固定期限

位置：[CanCyclicSendStopExecutor](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Executors/CanCyclicSendStopExecutor.cs:25)、[CanCyclicSendStopSetting](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/Models/CanCyclicSendStopSetting.cs:7)、[CanopenBus.Send](/D:/project/xTestPlatform_Plugin/CAN/CanPlugin/CANopen/CanopenSdoClient.cs:66)。

通用周期停止固定 5000 ms，Setting 无停止期限；CANopen NMT/PDO/SYNC 的简单发送仍经同步 `Send(...1000, CancellationToken.None).GetResult()`。这是**有界但不可配置/不响应本次发送等待取消**的缺口，不是无限死锁；它与新有序服务的配置期限不能混为一谈。

建议：旧步骤同样接入用户配置的停止/发送预算，走异步入口；保留后台实际 I/O 和发送未知隔离。验证 20 ms 与较长期限确实改变等待，不能仅断言字段存在。

### LCA-12．串口 Read/Query 的损坏设置会逃出执行器错误边界

位置：[SerialPortReadExecutor](/D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin/Executors/SerialPortReadExecutor.cs:17)、[SerialPortQueryExecutor](/D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin/Executors/SerialPortQueryExecutor.cs:17)。反序列化及 CurrentStep 解引用仍在 try 外。

损坏/迁移失败的设置不返回插件自己的中文 Error/完整错误信息，而是抛给宿主兜底。**这本身不能证明进程卡死**，但不符合手册执行器异常契约，且先前 CAN/Ethernet 的损坏设置回归未覆盖这两个入口。

建议：保护范围包含解析；使用实际执行器对空 CurrentStep、非法序列化数据、旧版本缺字段分别验证 Error，而非用外层测试捕获异常当通过。

## 条件性风险和必须保留的保护

| 项目 | 当前证据与边界 | 后续要求 |
| --- | --- | --- |
| OPC Stop 的同步取消回调 | `OpcUaDataAcqTask.RequestStop:139` 用同步 Cancel，StopAsync 在其后才 WaitAsync；托管 read 替身注册阻塞回调，40 ms 停止配置后约 120 ms 仍没返回。实际 SDK ReadAsync 是否有慢取消回调未实测。 | 请求取消本身也不能无界占用调用线程；跟踪 CancelAsync 的真实回调任务，停止总预算包含它。 |
| VISA Dispose 的同步 Cancel | `VisaSessionResource.Dispose:160` 可同步等外部注册回调，公共 owner API 替身已复现。但当前生产 FormattedIO 不使用该 token，主要消费者是 Task.Delay/退役等待，**不能据此说真实 VISA 驱动已卡死**。 | 作为 API 防御与未来消费者风险；不要扩大成已证实现场问题。 |
| CiA402 外部后端释放失败不可重试 | `CoordinatedAxisTransport:125` 一次性 `_innerDisposed=1`；fake 首次 Dispose 失败后第二次 DisposeAsync 仍旧异常、DisposeCount=1；AxisSession 的 release 也只建一次。当前 CANopen inner.Dispose 仅释放自身节点，主要影响可失败的外部 EtherCAT 实现。 | 实现 EtherCAT 时明确可重试释放状态；不要把这个边界当作当前 CANopen 必现锁死。 |
| 原生驱动永久不退出 | 当前 owner/退役表保留引用与物理占用，是正确的资源保护，不应为立即重跑清空表或关闭仍在用的句柄。 | 插件要限时结束调用方等待、给出阶段/actual/epoch；若要求损坏驱动下仍硬保证重跑，需要厂商 Abort/复位能力或进程隔离，不能仅靠 CancellationToken。 |
| 宿主顺序与线程 | 资源表只承诺停止时 Dispose，没有在本仓库证明全部线程调度、轴/总线的回收先后和整个 UI 消息循环。HTTP 证书导入、Socket Dispose 的极端系统等待也无现场证据。 | 完整平台测 stop→rerun→exit；先让依赖方停止提交并确认退出，再销毁传输。CiA402 只释放自己的租约，不应擅自关闭父 CAN。 |

## 全模块覆盖与没有重新误报的问题

| 模块 / 当前版本 | 步骤数 | 已审查运行生命周期 | 结论定位 |
| --- | ---: | --- | --- |
| CAN / 1.0.34 | 33 | 6 个原始/通用周期步骤、8 个 CANopen 基础步骤、6 个受控服务步骤、8 个 UDS 步骤、Flash、4 个 XCP；owner/路由/确认/NI-ZLG 关闭与其他适配器入口 | LCA-02/05/06/11；不删除未知发送隔离，不假定 TPDO 固定四路。 |
| CiA402 / 1.0.6 | 18 | Bind/Unbind、控制/读取、AxisSession、协调传输、CANopen 租约、外部 EtherCAT 工厂、故障日志/清理 | 当前 Dispose 已不再同步等 Close，步骤令牌未继续绑定会话；未发现新的 CANopen 高置信整轴清理死锁。外部释放失败与父传输顺序为上述边界。 |
| Ethernet / 1.0.20 | 14 | TCP 4、UDP 2、DoIP 4、SOME/IP 4；连接池代次、晚到工厂、关闭重试、帧读写 | LCA-08/09/10；现步骤用异步 Close，没有把 obsolete 同步兼容 API 误当成实际步骤调用。 |
| Http / 1.0.20 | 6 | Client Create/Close、Request、SOAP、JSON/XML；资源表门、临时对象、HttpClient 请求取消 | 本轮未发现新增高置信生命周期缺陷；不等于没有极端 OS/文件系统或宿主调度风险。 |
| LIN / 1.0.28 | 9 | Open/Close、Read/Write/WriteRead、Wakeup/Sleep、周期 Start/Stop；NI native 句柄、作业真实 Completion | LCA-01/06/07；Stop/Close 保留真实 I/O、检查 Stop/Clear 的修复仍在，不能回退成只 Cancel 就报告成功。 |
| Modbus / 1.0.20 | 6 | Connect/Disconnect、单/批读写；TCP/RTU 工厂、请求串行、transport interrupt、owner/退役 | LCA-01；未发现需重报的初始化失败丢 owner/晚到覆盖新代次；正常事务仍有迟到提交窗口。 |
| NiDaq / 1.0.20 | 10 | 配置/启动/停止、AI/DI/DO/Encoder/SyncRead；复合任务、Abort、释放与物理键、导出 | LCA-03/04；owner 内预算与真实 I/O 释放保护有效，不把 Release 内后台等 Abort 误报为 UI 同步等待。 |
| OpcUa / 1.0.20 | 10 | 连接/断开、单/批读写、订阅、采集 Start/Read/Stop；请求/activity/collector、Session 释放 | LCA-04、同步取消条件风险；恢复仍由用户序列实现，不新增自动重连。 |
| SerialPort / 1.0.28 | 5 | Open/Close/Read/Write/Query；事务门、余量组帧、端口退役、开失败回滚 | LCA-01/12；已提交同步 Read 未退出时继续保持引用不是新缺陷。 |
| VISA / 1.0.20 | 7 | Open/Close/Read/Write/Query/BatchWrite/WaitOPC；原生提交预算、完整事务、失败关闭/重试、资源键 | 已有单调提交预算排除 LCA-01 的直接影响；同步 Cancel 为条件风险，未发现当前生产调用链的新高置信句柄释放缺陷。 |

所有 118 个 StepTypeId/名称清单在源码基线 JSON 中。上述覆盖是静态调用链审查，不是“118 个步骤都在平台/设备上运行通过”。

## 验证记录与独立审查

### 本轮实际执行

| 检查 | 范围与结果 |
| --- | --- |
| `node artifacts/plugin-lifecycle-audit/Inventory.mjs` | 固定 10 模块、118 步骤、1080 文件指纹。 |
| `pwsh -NoProfile -File artifacts/plugin-lifecycle-audit/Check-DeliverySource.ps1` | 30 个 PE/PDB、1199 本地源码文档匹配；未加载原生驱动。 |
| `pwsh -NoProfile -File artifacts/plugin-lifecycle-audit/Run-DeliveryRegression.ps1` | 本轮重新执行现有交付的 9 项目离线回归，545/545、失败 0；不是新增缺陷已经修好的证据。 |
| CAN 旧版本核验探针重新执行 | 本轮复现 LCA-05；整连接关闭与同物理键可重开反例也通过观测，未发送设备报文。 |
| `RestartProbe` 离线 restore/build/run | 构建 0 警告、0 错误；复现 LCA-06 的首次失败/第二次可重开。原普通 restore 因沙箱读取用户 NuGet.Config 受限失败，经审批仅离线还原成功；没有安装新依赖。 |
| Shared 入口屏障探针 | 源链接当前实现，三次均复现 LCA-01；不是压力测试覆盖率。 |
| CAN/CiA402/LIN 假适配器探针 | 复现 UDS 同步入口阻塞、XCP 预取消仍发送、实际 LIN_Sleep 配置失效；另确认外部 CiA 后端释放失败的条件性边界。 |
| IO 源链接/托管替身探针 | 验证导出/消费者、驱动枚举与取消回调的等待缺口；没有 vendor 原生调用。 |
| 最终静态/交付检查 | 1080 个源码/项目/页面文件指纹未变；报告 46 个本地链接/行号有效；1170 个文本文件 UTF-8、C# 无 BOM/118 个编辑器描述/执行状态检查及 `git diff --check` 通过。 |

现有回归分布：CAN 153、CiA402 208、LIN 27、Modbus 21、NiDaq 21、OpcUa 39、SerialPort 10、VISA 29、Ethernet/Http/Shared 37。[本轮 9 份 TRX 和回执](/D:/project/xTestPlatform_Plugin/artifacts/plugin-lifecycle-audit/regression/receipt.json)。保留这些通过，同时将新交错补成后续修复的失败测试，不能把 545/545 写成发现已消除。

独立只读审查由 3 个原生子 Agent 按 ECC C# / 异常传播方法完成：CAN/CiA402/LIN 与 Shared/Ethernet/Http 均交叉检查非本人近期实现范围；五个 IO 模块独立读完整执行器/资源链。主 Agent 逐项读相关源码、核实范围、复现 CAN 恢复、核对程序集/源码与本轮回归并去重。报告与原始探针保存在 `artifacts/plugin-lifecycle-audit/audit-can-cia-lin/`、`audit-shared-net/`、`audit-io/`，不把实现者自测冒充独立意见。整合后又由非主 Agent 只读核对相关结论、矩阵和回执，未发现实质错误；两处证据措辞建议已采纳，未借复审扩大修改范围。

### 未执行 / 不作结论

- 未运行真实 NI/ZLG/其他 CAN、LIN、DAQmx、串口、PLC、ECU、仪器或电机；未验证驱动 Abort/Stop 的真实时延、总线顺序、位时序、物理停稳和硬件恢复。
- 未运行完整 xTestPlatform 的停止/立即重跑/解除绑定/退出，也未测 UI 消息循环。假适配器“调用线程被占”不等同于现场 UI 一定卡死；组件离线测试也不能替代整个平台。
- 未重建/替换本轮生产 core/UI，因为没有生产改动，已核对现有程序集来源并新鲜重跑相关测试；后续实施必须按最终更改重新构建 core+UI。
- 未测全仓覆盖率、未做依赖漏洞扫描/完整安全审计；本报告聚焦生命周期、阻塞与恢复，不宣称无其他功能或安全缺陷。

## 修复顺序与验收合同

建议顺序：LCA-01/02（不能晚发、调用方可退出）→ LCA-03/04（补完整等待边界）→ LCA-05/06（能清理但被历史错误阻断）→ 配置/错误契约项。先保留可重复的失败交错，再做最小实现；不是统一调大默认 timeout。

每个插件至少应满足：

1. `Running → Quiescing` 先冻结新提交；未开始的旧代次不得在期限或取消后启动。
2. 调用者在**用户配置**的总预算内返回 Error/Aborted；预算涵盖其承诺范围内排队、操作和收尾等待，内部超时不是 Aborted。
3. 已提交操作继续由旧 owner 追踪；`WaitingEnded`、`ActualExited`、`HandlesReleased` 分别记录。原生失败未知不能用“已停止”掩盖。
4. Dispose 只发起、持有并观察清理，不在宿主回收线程无界等待 I/O 或取消回调；显式 Close 用有限等待报告确切阶段。
5. 正常中断后立即重跑等待精确旧 owner 实际释放；已释放的历史错误不伪装通道仍占用，未释放时明确拒绝。迟到关闭不能影响新代次。
6. 后台故障、PartialCycle、未知发送要保留诊断与隔离；通信 ACK/任务退出不代表电机停稳，停止后监测 SYNC 不是残留运动目标。
7. 新字段按手册同步 Setting、Description、VM/页面和 ValidateWithContext；增加旧版本缺字段兼容与真实执行器预算测试。

现场最小验收矩阵：每模块正常结束、中断打开、中断读/写、中断后台采集/周期、关闭超时后晚完成、首次立即重跑、重复 Stop/Close、驱动/日志/文件输出失败、平台退出。运动类另验证控制权、停止反馈与物理安全，不靠软件探针宣布到位/停稳。

本轮没有实施上述修复，因此不能将本报告作为“插件优化全部完成”或“保证平台绝不卡死”的验收单。
