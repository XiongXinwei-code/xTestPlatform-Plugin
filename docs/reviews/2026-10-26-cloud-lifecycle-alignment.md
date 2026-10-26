# 云端生命周期对照、CAN 关断与监测勘误

任务等级 L3。本轮针对用户提供的两次 MonitoringSYNC 故障及“中断后首次重跑不能打开通道”，对照云端关断语义修复本地新增限制。没有读取远端 admin 机器上的原始 HTML，也没有连接真实设备；现场数字来自用户报告，源码、可重复屏障与构建证据来自本机。

**最新范围：用户要求先完成 CAN。其他插件实现已冻结，以下非 CAN 对照仅保存本轮已有工作状态，不作为本次成套交付或完成声明。最终优先验收和编译 CAN，其它插件的改动保留在工作区，待 CAN 验收后再处理。**

## 1. 核心结论与八项回执

此前将 CanClose 拒绝统称为“安全护栏正常起作用”不准确。步骤应执行自己的职责：CAN_Close 终止并释放本连接，不以前一步 ServiceStop 成功、停机命令 ACK 或电机停稳为前提。确有 SDK 契约支持的终态 Close/Clear，不能被历史业务状态永远挡住。

| 报告项 | 当前源码结论与改动 |
| --- | --- |
| 轻微超期即故障 | 确定的插件判据问题。监测发送已经确认后，旧代码仍将 LastDurationMs 与 PeriodMs 硬比较；20.0622 > 20 足以触发，不能据此归因为硬件。纯监测超过周期只计 SlowMonitoringBatches；真实确认超时/起始迟到仍故障，运动周期与点表错点规则未放宽。 |
| 超时贴着周期 | 等待的 deadline 本来由 CycleTimeoutMs 独立配置；错误来自成功后的第二个硬比较，并非已证明 deadline=period。保留用户发送总预算，不擅自改成 period×k。Timing 新增实际采用的期限、排程等待、适配器批次、返回续体及总耗时；适配器批次不是纯线缆时间。 |
| MonitoringSYNC | 此模式由插件主动发送配置的 SYNC，是生产者；不等待另一个 SYNC 生产者的消费窗口。监测仍从前批完成后计算下一间隔，不宣称硬实时节拍。 |
| ServiceStop 重复历史异常 | 仅在服务订阅/节点占用已释放、worker/回调实际退出、发送已确认且无 PartialCycle 时，步骤报告完成，Value 为完整 JSON，历史 Error/ShutdownHistory/Warn 仍在。只分类精确的成功关闭后重抛历史异常；非法期限、新异常、日志错误、真正关闭失败仍 Error。CycleStop 未改为忽略故障。 |
| false 标志导致锁死 | 两个标志不是 NI SessionInfo，CanClose 并没有按 MotorStoppedVerified 做判断。StopCommandsAcknowledged 只表示配置的停机 SDO 实际获答；MotorStoppedVerified 不能由通用 CAN 证明，保持 false。真正修复是 NI 错误的停止状态前置和父关闭被业务历史/回显账本阻断，不能靠改旗标解决。 |
| Receive.Running=true | 指独立连接接收侧，可与故障的发送 worker 同时存在；保留字段，加顶层 ServiceHealthy 与 ServiceShutdownConfirmed / ServiceShutdownState，避免将接收运行误读为服务健康或电机停稳。 |
| BackgroundError=null | 原 Receive.BackgroundError 仅指接收侧。保留其语义，加顶层 BackgroundError 汇总接收、周期与关断历史；ErrorSummary 包括 FailurePhase、LastDurationMs、实际 BatchElapsedMs、AdapterBatchMs、发送期限和 Overruns。失败批不会被前一次成功批耗时冒充。 |
| 队列数字 | 属不同层，不是重复计数。路由 FIFO 每 ID 容量64，PendingFrames 是当前总深度；LatestReplacements 是本订阅最新值替换次数；QueueStatistics 是适配器层。新增来源字段。4×64=256，4×(156−64)=368，4×(156−1)=620；120帧时对应224/476，差252=4×63。覆盖1/4/7路测试，未把TPDO路数写死。 |

旧 Overruns 是 worker 故障路径累加的兼容字段，并不是当前所有慢监测批的计数；新的 SlowMonitoringBatches 单独表达非致命慢批次。已有主步骤 Error 不会因后续关断成功而被抹除。

## 2. CAN_Close 的独立职责与 SDK 依据

流程为冻结本 owner 的所有新提交 → 取消并等待真实受控任务/回调/接收线程及 native I/O → 调用本 owner 的合法 SDK Stop/Close/Clear → 按实际返回确认句柄释放 → 解除物理占用。普通托管日志/表达式与真实硬件释放分别跟踪：物理已释放后旧 worker 仍保留并冻结，显式关闭继续按原总期限等其退出，不能假称 HostExited。

缺回显、历史停机失败、PartialCycle 仍禁止恢复运动、正常 CycleStop 或严格 ServiceStop；它们不再永久阻止父连接的 SDK 终态关断。关闭不补写 submitted/confirmed，不保证曾提交报文撤回，不证明电机停稳。真正 native/pump 尚未退出，或 SDK 关闭尚未完成/失败，仍保留相应句柄和物理 owner，步骤报告未完成；不使用 ForceReset、设备复位、硬清句柄或伪造状态。

- NI 官方 [nxStop / nxClear](https://github.com/ni/grpc-device/wiki/NI-XNET-General-Functions#nxclear) 规定 Clear 自行停止通信并释放资源，显式 Stop 可选。Normal Stop 可异步完成，因此不要求 Clear 前单次 SessionInfo=0，不要求先收到所有回显。对本 RX/TX 分别收集 Stop/Clear 结果，成功一个才清一个句柄，错误仍保留；不使用影响其它会话的 InterfaceOnly。
- 本机 nixnet.h 的 Scope/SessionInfo 类型与常量已核。SDK 头文件路径与原有声明不做猜测替换；本轮不依赖真实 NI 硬件。
- Vector 官方 [XL Driver Library 手册](https://cdn.vector.com/cms/content/products/XL_Driver_Library/Docs/XL_Driver_Library_Manual_EN.pdf) §3.3.13 明确 ClosePort 也停用其通道。Deactivate 错误不阻止合法 ClosePort，ClosePort 失败保留端口与驱动归属。
- ZLG 官方 [CAN_lib.zip](https://www.zlg.cn/data/upload/software/Can/CAN_lib.zip) 内 V1.21 手册及示例在接收线程退出后直接 CloseDevice，发送失败也进入最终关闭。ResetCAN/缺回显不再成为 CloseDevice 的业务前置。只关闭本 adapter 拥有的设备句柄，SDK 成功后才退休旧代次未决票据，保留未知诊断，不伪造确认。

SDK 文档支持上述终态关闭，不等于允许并发 native read/write 尚运行时任意销毁句柄。不能把本次修正演变成无条件强清。

## 3. 十插件云端对照与实际改动

| 模块 | 对照后的行为 |
| --- | --- |
| CAN | 修正监测判据、NI/ZLG/Vector SDK 关断前置；父关闭独立执行，服务历史不阻止其它资源收尾；普通后台任务与物理释放分层；新增顶层健康与统计域。 |
| CiA402 | 跟随私有 CAN 依赖更新；不将轴解绑改成关闭父 CAN，不增加运动控制字或自动恢复。 |
| LIN | 参考云端 Close 本通道原则，普通 managed worker 不占已释放句柄；旧 worker 强引用保留/冻结。注册按键互斥，跨连接同名任务最终删除另取短任务键租约，防止旧 Close 删除新任务。 |
| HTTP | 参考 Dispose 是合法中断入口，取消与 SDK Dispose 独立启动，再确认所有 actual/回调/释放完成。长操作按 epoch+大小写一致名称互斥，最终发布/删除另用跨 epoch 短 fence，不包慢构建或关闭。 |
| Modbus | TCP 现有关闭入口保持；RTU 新增本串口 PurgeComm 对应的 SDK 中断/Close 后真实退出确认，禁止关闭中的 NModbus 重试继续提交。中断失败仍尝试最终合法释放并保留原错误。 |
| SerialPort | 同上，配置的读写期限不被偷偷截短；增加实际释放成功标志，封住 Dispose 成功与 gate.closed 发布间重复 abort 的窗口。 |
| Ethernet | 云端及本地 TCP/DoIP 已具备关闭被捕获实例的入口，不额外增加“健康才可关闭”的条件；保持精确 owner。仅同步私编公共层版本。 |
| NI-DAQ | 保留驱动 Abort 与真实 I/O 退出；AO 仍按显式输出收尾策略，不能并发 Abort 改变输出语义。没有 DeviceReset 或新增猜测控制。仅同步私编公共层版本。 |
| OPC UA | 云端 Session.CloseAsync/Dispose 对照已读取；保留精确 Session/实际请求与订阅退出，重连仍由序列执行，不引入共享同设备全局断开。仅同步私编公共层版本。 |
| VISA | 云端 Remove/Dispose 对照已读取；IVI 规范并不提供跨厂商 Dispose 中断任意同步 FormattedIO 的通用保证，因此保留真实同步调用退出边界，不声称取消已撤回 I/O。仅同步私编公共层版本。 |

Shared PendingIoGate 增加显式异步释放通路，避免 Serial/RTU 用同步 GetResult 等待需要线程池执行的 shutdown 而饥饿；gate 持有到实际释放完成才 closed，原 Action API 的异常/并发/可重试行为保持。

CAN 最后另以有效 RED 证明 NI/ZLG 的接收泵隐藏1秒失败会覆盖配置5秒关闭期限。现在父连接后台持续拥有实际收尾，前台仍按用户配置有界等待；短30ms等待超时不撤销实际清理，泵阻塞超过1秒仍保留原5秒任务，真实放行后自动关闭，不需要再发起关闭才触发清理。仍未退出时不 Clear/Close，不虚报已关闭。

没有证据可以承诺任意失效驱动、永久不返回的 native 调用都能立即重开；这种情况与被错误业务前置挡住合法 SDK 关闭不同，必须如实报告实际未退出。

## 4. 兼容、基线及复审

本轮分支 codex/cloud-lifecycle-alignment-20261026；起点 b9eb840d（前轮产品61b5fa8e），原 develop-backup-20261024 保留。已重新 fetch，origin/master=5351a0a9d66594455b292fe65308303488a07537；手册本地/远端 blob=9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0。云端代码用于校正职责，不回退到忽略返回值、提前清句柄或静默吞错。

设置字段、默认期限、步骤 ID、PDO/UDS 字节不变；快照字段只追加。CAN_RuntimeContracts 为同构建权威 DTO，序列可继续读取旧字段。ServiceStop 成功 Value 现在是 JSON 字符串而非原简单操作文本；依赖旧 Value 精确文本的脚本应读取 ServiceShutdownState 等字段。历史故障继续在 JSON 与 Warn 中；原内部 CloseAsync 仍可抛出精确历史异常，步骤只分类已证实成功关闭的历史结果。

CAN 核心/UI 递增至1.0.40。本轮早期还在工作区拟同步递增其它核心/UI（Shared私编进入各核心，CiA402携带私有CAN）：CiA4021.0.12，LIN/SerialPort1.0.34，Modbus1.0.25，其余1.0.26；这些后续事项随用户最新要求冻结，不混入本次 CAN 独立交付。成套回退核心/UI/依赖，不混装旧私有 CAN；不部署、不推送、不操作设备。

三轮软件勘误：第一轮保存有效 RED 和 SDK/云端证据；第二轮独立只读审查并修复异常来源误分类、取消/最终关闭组合异常丢失、纯监测未知发送被物理关闭错误标成已确认，以及父关闭回调历史未写入服务快照；第三轮对最终 CAN 源码完整回归、核心/UI 打包、33步骤身份和PE/PDB指纹核验。早期十插件软件回归结果保留，但不冒充用户最新范围下的其它插件交付。编译错误、测试夹具错误以及旧监测判据假设不计作产品缺陷复现。

ECC 能力实际执行包括 planner/architect 分层与 SDK 风险分析、TDD 失败屏障、C# 调用链审查、silent-failure 异常传播复核、contract-first 追加 DTO 兼容检查、verification-loop。独立只读审查由非作者原生子 Agent 实施；审查无设备操作，最终整合由主线程负责。覆盖率未测量，不宣称达标。实际平台中断/退出调试窗口重入和真实 NI/ZLG 总线时序、设备停稳仍未验收。

## 5. 最终软件验收

本轮 CAN 软件范围完成，尚无未关闭的已确认软件复审问题；不等价于所有驱动/实卡/现场工况都已证明无缺陷。其它插件按用户要求冻结，未加入本次插件目录。

| 检查 | 实际结果 |
| --- | --- |
| 最终完整 CAN 回归 | 599/599，失败0、跳过0；包括本轮所有新反例及真实WPF页面组件，TRX与日志在交付verification目录 |
| CAN核心/UI win-x64整目录Rebuild | 0警告、0错误；不使用测试输出冒充部署目录 |
| 插件工厂/编辑器及身份 | 33/33；与前轮CAN步骤ID、显示路径逐项相同 |
| PE/PDB与当前C#来源 | 3个二进制（核心/UI/测试）、317个源码文档出现项SHA256匹配；不替代XAML或原生SDK硬件验证 |
| UTF-8/编辑器/状态检查 | Verify-PluginSources通过，1411文本文件/120独立编辑器描述；C#无BOM，差异空白检查通过 |
| 测试文件污染/完整性 | 插件目录不含xUnit/TestHost/Tests程序集；161文件、50,257,478字节，逐文件SHA256清单已生成 |
| CAN私有副本 | 可选CiA402依赖更新中的CAN DLL/PDB与主CAN逐文件一致，只同步CAN，不修改CiA402步骤或版本 |
| 实卡/完整平台调试退出重入 | 未执行；没有可安全运行的宿主测试工程，NI/ZLG/Vector真实时序、电机状态和完整宿主加载待现场验证 |

交付目录：`D:/project/xTestPlatform_Plugin/artifacts/Release/2026-10-26-CAN-1.0.40/Plugins/CAN`。原目录需在平台退出后整目录备份/替换；如果`Plugins/CiA402`存在私有CAN，使用同交付根目录的`DependencyUpdates/CiA402`同步两个CAN依赖文件，不能将补丁目录当成完整CiA402插件。

源码产品提交：`f5962f06057f01e2009f957d736ee9f2f5a0dd1a`，本地分支`codex/cloud-lifecycle-alignment-20261026`，未推送。CAN/Shared及构建输入作用域已提交且无未提交差异；工作区其它插件保留冻结改动，清单据实标记而不声称整个工作区干净。打包后仅新增本报告，不改已编译产品源码。

| 程序集 | AssemblyVersion / FileVersion | MVID | SHA-256 |
| --- | --- | --- | --- |
| CAN.StepPlugin.dll | 1.0.40.0 / 1.0.40.0 | 1924cb4b-3792-48f0-92df-4f55aaa359c5 | f7a26d0307cc5e4e1ea41ecf3c60bcd00caf6d918400ebd5047739e599a6ef19 |
| CAN.StepPlugin.UI.dll | 1.0.40.0 / 1.0.40.0 | d504d588-be44-4444-94f7-2b6d95a5db1a | b761aad117b59d454e1dabba1aeeaa133d5f583b759d79c81dab03dc4628074b |

主要代码位于 CAN/CanPlugin/Runtime/CanopenCyclicService.cs、CanConnectionResource.cs、CanRuntimeContracts.cs，Adapters/NiXnet、Adapters/Zlg、Adapters/Vector，Executors/CanCloseExecutor.cs 与 UI/Views/CyclicServiceEditorView.cs。与远端master的264项CAN/Shared文件差异清单见同目录`2026-10-26-can-vs-master-files.csv`；该数字包含前轮已有差异，不冒充本轮新增修改数。
