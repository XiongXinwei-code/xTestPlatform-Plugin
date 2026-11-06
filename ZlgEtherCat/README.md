# ZLG EtherCAT 步骤插件

独立的 ZLG EtherCAT 插件，使用厂商 PCIe SDK 完成主站、PDO、CoE/SDO、I/O、伺服和厂商专有操作，不依赖 CAN 或 CiA402 插件。当前目标板卡为 HPCIe-2E；现有 SDK 的实际兼容性、固件与驱动组合尚未通过该板卡实测。

## 功能目录与操作选择

工具箱提供 **97 个步骤，完整覆盖 109 个操作**：90 个步骤固定执行一个操作，7 个步骤仅合并操作方式和用途高度相似的功能。打开/关闭、启动/停止、使能/禁用、读/写分别独立；不会只因为属于同一大类就合并。单功能页面没有功能下拉框；组合页面才用下拉框选择，一次执行仍只调用一个操作。

运动控制保留在 `ZlgEtherCat → Motion` 次级目录，绑定、使能、模式设置、位置/速度/转矩运行、回零、停止、去使能、解绑分别为步骤；同类轴状态/反馈读取和同步目标写入各保留一个组合步骤。周期读取池与后台常驻读取池分别提供创建、添加、更新、停止项、状态、关闭六个独立步骤，两个固定功能池的运行模型不变。

本版本按新的操作边界提供入口，不提供旧的大类步骤或旧序列迁移。单功能 StepTypeId 与操作同名，例如 `ZlgEtherCat.Open`；组合步骤的具体 Operation 见文末完整目录。

## 平台助手读写参数

全部功能具有公开的 StepTypeId 和强类型 `Operation` 枚举；单功能步骤只有一个固定枚举值，无需切换。全部参数是公开、强类型、可读写和可序列化的字段。插件说明逐操作列出适用参数、表达式结果类型、默认值、前置条件、目标范围与返回值。助手无需操作 UI、编辑私有数据或使用不透明 JSON。

例如读取指定从站的 SDO：选择独立的 `ZlgEtherCat.ReadSdo` 步骤，配置 `ConnectionName`、`SlaveIndex` 或 `SlaveIndexExpression`、`Index`、`SubIndex`、`DataType`、`Capacity`、`TimeoutMs` 和可选 `ResultVariable`。写入时使用独立的 `ZlgEtherCat.WriteSdo` 步骤，并提供 `Data` 等该操作适用参数。先完成配置并读回核对，再执行步骤；改参数本身不调用设备。

未选参数可保存并读回，但不参与本次操作的业务校验、求值或请求。当前宿主会收集并预编译全部表达式字段；该过程不求值，编译失败不阻断其他所选操作。切到对应功能后，错误表达式必须通过校验才能执行。未知操作枚举、错误类型和不可为空字段的 null 在存储边界拒绝。

## 多个相同模块如何定位

从站参数读写必须指定目标。`ConnectionName` 选择已打开的板卡通道，`SlaveIndex` 选择该通道当前 ENI 中从 0 开始的从站索引；两个相同型号仍是两个独立索引。它不是板卡通道号、ENI 固定站地址、别名，也不是链路拓扑里从 1 开始的分支接线位置 `PhysicalPosition`。更改 ENI 或接线后必须重新核对，不按型号猜测目标。

先执行 `EnumerateSlaves`，将结果写入已声明变量，检查每行的 `SlaveIndex`、`FixedAddress`、`Active`、`Ready`、`Info` 和 `Range`。离线从站仍保留其索引，身份信息为 null，不伪装为另一台在线设备。序列号可能为 0；型号相同、序列号缺失时，软件枚举不能独立证明实物标签，需与实际接线和 ENI 对照。

| 操作 | 目标与读写范围 |
| --- | --- |
| `ReadSdo` / `WriteSdo` | 一个指定从站的 `Index:SubIndex`；不是全总线参数批量读写。`CompleteAccess` 是该从站同一对象的多个子索引，仍然只有一个目标从站 |
| PDO、数字量、模拟量、两个读取池 | 指定从站输入或输出映像内的相对字节偏移；`Offset=0` 是这台从站该方向映像的开头 |
| `StartAcquisition` | 唯一后台采集器读取完整主站输入映像；各读取项再按自己的从站索引、偏移和类型提取数据 |
| 运动步骤 | 先用 `BindAxis` 将 `AxisName` 绑定到指定 `SlaveIndex`；后续运动步骤使用该连接下的轴名 |
| 板载 I/O | 板卡自身端口，不能用它读取 EtherCAT 从站模块；HPCIe-2E 未开放这些板载端口步骤 |

例如，枚举结果显示两台相同输入模块分别为 `SlaveIndex=0`、`SlaveIndex=1`，输入映像起点分别为 0、4 字节。读取各自第一个 Int16 字段时，两步都填 `Offset=0`、`Length=2`、`DataType=Int16`，但从站索引分别填 0 和 1，结果分别写入 `Locals.ModuleA`、`Locals.ModuleB`。第二台不能填写 `Offset=4`，那会表示它自身映像内的第 4 字节。此处地址仅作规则示例，实际数值以自己的 ENI 和枚举结果为准。

普通从站步骤可在 `SlaveIndexExpression` 中引用循环变量；留空时沿用原 `SlaveIndex` 常量。非空时表达式优先，求值结果仍须在合法范围内。动态数字/模拟输出与板载 DO 同样可用可选 `ValueExpression`，常量与表达式两种输入方式都可使用。

## 映射的数据类型与单位

先用 `ReadPiRange` 读取指定从站的映像范围和 ENI 字段。`InputFields` / `OutputFields` 包含字段名、ENI 类型、位宽、全局位偏移、从站相对位偏移及是否字节对齐。`HasMetadata` 只表示该从站范围内至少有一个字段，`InputMetadataAvailable` / `OutputMetadataAvailable` 分别表示对应方向是否有字段，均不保证完整覆盖。没有元数据时需手工核对实际映射；有类型信息也不等于已确认对象索引或实物身份，`ObjectIdentityVerified` 固定为 false。

普通 PDO 选择的数据类型必须与实际映射一致；已有 ENI 字段会校验边界、宽度、符号和类型。`UInt16` 与 `Int16` 都是两字节，但 `FF FF` 分别是 65535 与 -1。标量使用小端。非字节对齐的数字位应使用按位/数字量步骤；`Boolean` 标量表示整个一字节的布尔值，不等同于一个 PDO 位。`Bytes` 是原始字节访问，不执行类型推断或单位转换。

`WritePdo`、`WritePdoMasked`、`WriteSdo` 默认 `DataType=Bytes`，接受 `byte[]` 表达式。选择数值类型后，`Data` 可填写数值表达式，例如 Int16 的 `-100`、UInt16 的 `0x1234`；Boolean 要求 bool。64 位整数保持整数精度，不能先转为 double。类型错误、整数小数部分或越界会报错，不会截断或回绕；掩码仍为与编码后数据等长的 `byte[]`。

运动映射采用本插件支持的 CiA402 固定布局，不允许随意选择另一种类型来解释标准对象：

| 映射对象 | 数据类型 | 字节数 |
| --- | --- | --- |
| 控制字 `0x6040:0`、状态字 `0x6041:0` | UInt16 | 2 |
| 模式命令 `0x6060:0`、模式反馈 `0x6061:0` | Int8 | 1 |
| 目标/实际位置 `0x607A:0`、`0x6064:0` | Int32 | 4 |
| 目标/实际速度 `0x60FF:0`、`0x606C:0` | Int32 | 4 |
| 目标/实际转矩 `0x6071:0`、`0x6077:0` | Int16 | 2 |

运动字段偏移为**完整主站对应方向映像的绝对字节偏移**，`-1` 表示未映射；必须字节对齐，并落在绑定从站的范围中。有 ENI 字段信息时检查类型及宽度；该方向有字段但目标未命中时拒绝绑定，不能将部分元数据缺失当作验证通过。变量名称不能证明它就是指定对象，仍须核对驱动的 PDO 对象映射。驱动不采用此布局时不能硬套本插件运动步骤。厂商对象示例可参考 [Kollmorgen EtherCAT 通信手册](https://www.kollmorgen.com/sites/default/files/public_downloads/AKD%20EtherCat%20Communications%20Manual%20EN%20%28REV%20K%29.pdf)，最终以实际驱动对象字典为准。

模拟量输入按 `工程值 = 原始值 × Scale + Bias`，输出按 `原始值 = (工程值 − Bias) / Scale`；整数原始值如需取整，应在表达式中明确处理。运动位置、速度、转矩和回零参数使用驱动原始单位，不自动换算为毫米、转每分钟或牛米。

## 主流程

1. `EnumerateBoards` 查询板卡，用 `Open` 按 `ID` 索引、`AL` 别名或 `NO` 序列号打开指定通道。
2. `ConfigureReinit` 指定映像初始化策略；`StartBus` 加载 ENI。`ExpectedCycleUs` 默认 500，250/500 μs 必须与 ENI 一致；`RequireDc` 默认开启。
3. `ReadEniInfo`、`ReadPiRange` 检查从站与映像范围。`ConfigureExchange` 选择交换模式并显式提供完整输出初值。不能假定全零对设备安全。
4. `RequestState` 请求 OP 并确认状态/DC；`EnablePi` 使能过程映像交换。
5. `StartAcquisition` 配置共享 SDK 线程优先级与 RX 告警阈值后启动通道常驻采集。Queue 模式由唯一 SDK 工作循环调用 `EcatPIInputQueuePop`，再发布完整本地输入快照。
6. 按需要创建周期读取池、常驻读取池，添加读取项；也可直接读取 PDO、操作 SDO、I/O 或绑定轴。
7. 退出时，先执行 `BeginExit` 锁存退出状态，停止接受新的业务写入和运动请求；现有输入读取和退出步骤仍可使用。再完成轴停止确认、去使能、解绑，停止读取项并关闭两个功能池；停止周期输出、停止采集、禁用过程映像交换，按需要请求 INIT，停止总线，最后关闭指定通道。`BeginExit` 本身不证明运动已经停止。

纯输入总线的 `InitialOutput` 使用空字节数组，插件不提交零长度输出；纯输出总线不启动输入采集或读取池。若使用受支持型号的板载 PWM/脉冲/锁存，必须先按对应停止步骤结束，再关闭通道；同板资源由连接独占，不能让另一通道改写正在使用的板载配置。

启动总线会复位部分 SDK 配置；就绪丢失超时、自动清错等配置应按厂商说明在启动后重新应用。状态请求和 SDK 提交成功不等于物理动作或功能安全停止得到证明。

## 两个独立读取功能池

每个通道有周期读取、常驻读取两个独立工作循环。命名的功能池和读取项是配置集合；添加、更新、停止读取项不创建线程、任务或计时器。每类合计最多 1024 项，单个命名池另受 `MaxItems` 限制。

| 功能 | 周期读取 | 常驻读取 |
| --- | --- | --- |
| 创建/关闭 | `PeriodicPoolCreate` / `PeriodicPoolClose` | `BackgroundPoolCreate` / `BackgroundPoolClose` |
| 添加/更新 | `PeriodicPoolAddItem` / `PeriodicPoolUpdateItem` | `BackgroundPoolAddItem` / `BackgroundPoolUpdateItem` |
| 停止指定项 | `PeriodicPoolStopItem` | `BackgroundPoolStopItem` |
| 查询状态 | `PeriodicPoolStatus` | `BackgroundPoolStatus` |
| 触发方式 | 每项 `IntervalMs` 到期读取最新快照 | 收到新快照后读取最新帧 |

读取项显式设置 `PoolName`、`ItemName`、从站索引、偏移、长度、数据类型、最大本地年龄及已声明的 `DataVariable`。功能池只能修改指定变量，不派生变量路径。添加/更新失败、变量写入失败或数据无效均须查看状态中的错误；失败项需要显式处理后更新或停止。

常驻读取采用最新帧缓存，消费者来不及时会合并中间帧，`SkippedSamples` 表示该读取项未发布的软件采样序号差额，不是 EtherCAT 总线硬件丢帧统计。周期读取允许重复读取同一张尚在有效期内的快照。需要有界历史时使用 `ReadHistory`，不能将最新值池当作无损逐帧记录器。

`StartAcquisition` / `StopAcquisition` 管理原始采集；功能池创建/关闭管理读取配置，两者独立。存在依赖读取池时不能直接停止采集，需先关闭池。

这两个读取池目前只提取输入 PDO，不轮询 SDO 参数，不读取输出映像或诊断。定期读取同一从站的 SDO 应在序列中明确安排 `ReadSdo`，并遵守周期输出期间的邮箱限制。

在序列循环里等待下一帧时，使用 `WaitNextPdo` 并开启 `ReturnSample`：结果的 `Value` 为所选字段值，`Sequence` 为本地软件序号。下一次把上次 `Sequence` 填入 `AfterSequenceExpression`；首次可用 0。`ReadPdo` 同样可选择返回此样本结构。默认关闭该选项，兼容原先只返回值的配置。返回样本还含从站索引、相对偏移、长度、类型与本地接收时间；它不证明硬件周期未丢帧。

## 采集优先级与 RX 队列诊断

使用独立的 `ZlgEtherCat.StartAcquisition` 步骤，可以直接通过编辑页或平台助手配置以下公开字段。配置变化需要先完成依赖池/轴退出并停止采集，再重新启动；不会为读取项或告警新增线程。

| 参数 | 默认值 | 允许值与作用 |
| --- | --- | --- |
| `AcquisitionThreadPriority` | `Normal` | `Normal=0`、`AboveNormal=1`、`Highest=2`；作用于该通道整个共享 SDK 工作线程，包括输入采集、SDO 和输出 |
| `RxHighWatermarkPercent` | 80 | 1～100，RX 队列高水位百分比 |
| `RxBacklogWarningMs` | 10 | 1～60000 ms，持续积压的告警时间阈值 |
| `RxAlarmRepeatMs` | 1000 | 100～3600000 ms，该通道相邻队列通知（含恢复）的最小间隔 |

启动时使用 .NET `Thread.Priority` 应用优先级并读取确认，停止采集时恢复启动前的优先级。`Priority.Requested` 是本次请求的插件枚举，`Actual` 是读回的 .NET 优先级名称，`RestoreTarget` 是恢复目标，`Error` 保留设置/恢复失败；尚无对应值时可为 null。系统调度仍由 Windows 决定，提高优先级不保证 250/500 μs 周期或硬实时执行，也会影响同一工作线程中的其他 SDK 访问。依据：[Microsoft 线程优先级说明](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.priority?view=netstandard-2.0)。

`ReadAcquisitionStatus` 和两类 `PoolStatus` 返回 `Priority`、`RxQueue` 和时间来源元信息。采集状态读取只读缓存，不调用 SDK，因此在 SDK 阻塞或通道隔离时仍可查看最后的观测与错误，直到连接关闭。`RxQueue` 深度是最后观测值，必须同时检查 `ObservationAgeMs` 与 `MaxPollGapMs`；SDK 阻塞时，队列观测和告警本身也会延迟。告警通知使用已有后台池循环，不在 SDK 线程里调用宿主日志。

宿主日志变慢会延迟该后台池的变量发布，但不会占用 SDK 采集线程。关闭时设备清理与已进入的发布/日志退出分别进行；只有二者都退出才结束关闭并释放通道归属。日志异常记录在 `NotificationError`，新一轮采集不会继承旧回调的通知失败。

`RxQueue` 的主要字段如下，时间戳均为主机单调时钟刻度，不能解释为硬件采样时间：

| 字段 | 含义 |
| --- | --- |
| `Available`、`Monitoring` | 是否处于容量已知的 Queue 模式，以及当前是否启用监测 |
| `Capacity`、`CurrentDepth`、`PeakDepth` | 容量、最后观测深度和本轮峰值 |
| `HighWatermarkPercent`、`HighWatermarkDepth`、`BacklogWarningMs`、`AlarmRepeatMs`、`LatestOnly` | 本轮监测阈值和主动清旧配置 |
| `HighWatermarkActive`、`BacklogWarningActive`、`OverflowSuspected` | 当前高水位、当前持续积压和锁存的疑似溢出 |
| `HighWatermarkEvents`、`BacklogWarningEvents`、`FullQueueObservations` | 高水位/积压告警事件段数量，以及满深度观测次数；不是硬件丢帧数 |
| `LastObservationTimestamp`、`ObservationAgeMs`、`MaxPollGapMs`、`LastRecoveryTimestamp` | 最后队列观测时刻、距观测的时间、观测最大间隔和最后恢复观测时刻 |
| `LastAlarm`、`NotificationError` | 最近告警摘要、通知失败详情；重复通知会节流并可合并，通知失败不能当作告警已送达 |
| `HardwareDroppedFrames`、`HardwareDroppedFramesAvailable` | 固定为 null / false，硬件丢帧数量不可用 |

Queue 模式下容量未知时仍记录深度和持续非零积压，但禁用依赖容量的高水位/满判定；非 Queue 模式不进行队列告警。`Available=false` 不能单独解释为“没有积压”。

高水位和持续积压状态随观测变化；`OverflowSuspected` 只锁存“曾观察到可能满队列”的疑似证据，不证明硬件已丢帧，`HardwareDroppedFrames` 始终为 null。成功启动新一轮采集时重置诊断；停止后保留证据并标记 `Monitoring=false`，不能把停止当作异常恢复。

`LatestOnly=true` 是主动清理旧数据的策略，与设备或驱动丢帧不同。`ReadHistory.Dropped` / `DroppedMeaning="LocalHistoryEvictions"` 只统计本地历史缓冲淘汰；池项的 `SkippedSamples` 只统计跳过的软件序号。上述计数不能相加当作硬件丢帧数。

## 运动控制

运动步骤位于 `ZlgEtherCat_Motion` 次级目录，独立实现 CiA402 状态转换。先用 `BindAxis` 绑定从站、模式白名单和 PDO 映射，再复位故障、设定并读取模式、使能、下发位置/速度/转矩或回零指令，读取反馈及诊断，最后停止、去使能、解绑。

PDO 映射使用绝对映像字节偏移；普通 I/O/PDO 步骤使用从站映像相对字节偏移。必须按实际 ENI 与驱动器对象字典核对，不能仅按示例数值绑定。所有运动参数使用驱动器原始单位。CSP/CSV/CST 目标写入只是一次提交，不代表运动完成。

无 PDO 映射的反馈使用 SDO 顺序读取，`Sequence=0`，时间戳在多次对象读取完成后由主机记录，各字段不保证原子一致。CSP/CSV/CST 停止必须显式选择 `QuickStop=true` 并核对驱动的停止选项；停止反馈确认不能替代硬件限位、急停或安全链。

运动 PDO 输入和输出均要求 Queue 模式；每条控制字更新等待固定工作循环完成 SDK 入队，避免故障复位等上升沿被缓存合并。回执不证明驱动已收到，状态确认仍由运动步骤完成。周期输出期间不允许 SDO/邮箱等长调用阻塞队列补充；此时轴控制和反馈须配置 PDO。普通参数读写在停止周期输出后执行。

## 周期和数据语义

250/500 μs 是 ENI/DC 与板卡固件的 EtherCAT 总线周期。Windows 后台读取和目标更新不是 250 μs 确定性调度；本插件不以普通线程周期宣称硬实时 CSP/CSV/CST 控制。

队列输出按 SDK 报告的容量填充最多约 10 ms 的前瞻帧，容量和命令有效期会进一步限制该数量。命令更新、停止可能排在已入队帧后；停止软件续发不会撤回旧帧。每条周期轴分别检查目标有效期，其他轴不能替失效轴续期。异步映像读取的序号只表示软件读取次数，`HasQueueEvent=false`；不能用它宣称新总线帧或运动反馈的新鲜度。

标量按小端解释，字节长度必须与类型匹配。`Bytes` 返回原始 `byte[]`；写入使用 `byte[]` 表达式，可写十六进制字面值，如 `new byte[] { 0x12, 0x34 }`。位读取明确配置起始位与位数。`MaxAgeMs` 约束主机本地接收年龄，不代表设备真实采样年龄。

输入快照、带序号的 PDO 结果和运动反馈保留 `ReceivedTimestamp`，并公开只读元信息：`TimestampSource="HostMonotonicReadCompletion"`、`TimestampFrequencyHz=Stopwatch.Frequency`、`HardwareTimestampAvailable=false`、`HardwareTimestamp=null`。这些是结果说明字段，不是需要助手写入的配置参数。时间戳以主机读取完成的单调时钟刻度记录；刻度差除以频率才得到秒数，不能当作 UTC 日期。频率来自运行机器的计时器，依据：[Microsoft Stopwatch.Frequency 说明](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch.frequency?view=net-5.0)。

`LocalAgeMs`、状态中的 `AgeMs` 和池状态中的 `InputAgeMs` 只计算主机读取完成后的时间，不包含数据此前在 SDK RX 队列中的等待，也不等于硬件采样年龄。采集状态明确返回 `HardwareSampleAgeMs=null`；不能根据本地年龄小，就判断队列里读出的数据一定是新采样。

`TimeoutMs` 是步骤总期限。只有调用方取消返回 Aborted；内部超时或失败返回含中文上下文的 Error。软等待结束不代表 SDK 调用已退出或设备撤销动作。未确认的通道保持隔离，关闭等待真实工作退出后才释放物理通道归属。

## 厂商专有能力和验证边界

包含 ENI 校验、过程映像访问模式、输入/输出队列、就绪丢失与自动清错、看门狗及看门狗映像、强制输出、FoE、从站内存/EEPROM、抓包、板载 I/O/PWM/脉冲/锁存、板卡文件与维护复位等独立步骤。板载 I/O 能力按已核实的 PCIe-1E16 支持范围限制，不能从 HPCIe-2E 名称推断也支持这些端口。

HPCIe-2E 的厂商能力表标记 PDO 看门狗不支持，与通用 SDK 的接口可用性存在差异。本版本对 HPCIe-2E 拒绝五项看门狗操作，不以该接口提供硬件保护承诺；型号能力请核对 [ZLG 官方产品页](https://www.zlg.com/ethercatnet/ethercatnet/product/id/357.html)。

软件构建、离线 SDK 仿真和 WPF 编辑器测试与现场验收分别记录。尚未执行 HPCIe-2E、真实从站、DC 时序、250/500 μs 连续运行、总线断线、驱动停止与物理安全链验证。实际交付状态以本次验收记录为准。

正常退出按上面的运动停止顺序执行。执行中断时自动清理会停止发布、等待在途调用退出、尝试关闭通信；它不自动推断驱动停止策略，也不宣称停稳。读取项停止成功后不会再发布该项；宿主在回收执行时应等待旧通道清理完成再复用变量上下文。当前 SDK 未提供“检查执行代次并原子写变量”的接口，清理恰好发生于检查与宿主变量写入之间的窗口仍需宿主配合。

插件管理器中的步骤列表由编译后的程序集自动提取。参数类型、默认值和使用条件请查看对应步骤的说明及编辑页面。

## 包含的步骤

| 步骤 | StepTypeId | 操作 |
| --- | --- | --- |
| 枚举板卡 | `ZlgEtherCat.EnumerateBoards` | 固定：`EnumerateBoards` |
| 打开板卡通道 | `ZlgEtherCat.Open` | 固定：`Open` |
| 开始退出流程 | `ZlgEtherCat.BeginExit` | 固定：`BeginExit` |
| 关闭板卡通道 | `ZlgEtherCat.Close` | 固定：`Close` |
| 配置映像初始化策略 | `ZlgEtherCat.ConfigureReinit` | 固定：`ConfigureReinit` |
| 加载 ENI 并启动主站 | `ZlgEtherCat.StartBus` | 固定：`StartBus` |
| 停止主站 | `ZlgEtherCat.StopBus` | 固定：`StopBus` |
| 切换并等待主站状态 | `ZlgEtherCat.RequestState` | 固定：`RequestState` |
| 主站诊断读取 | `ZlgEtherCat.MasterDiagnostics` | 下拉：`ReadMasterStatus`、`ReadDiagnostics`、`ReadLinkDiagnostics`、`ReadLastError` |
| 读取 ENI 信息 | `ZlgEtherCat.ReadEniInfo` | 固定：`ReadEniInfo` |
| 核对运行 ENI | `ZlgEtherCat.CheckEni` | 固定：`CheckEni` |
| 读取从站 PDO 范围 | `ZlgEtherCat.ReadPiRange` | 固定：`ReadPiRange` |
| 配置 PDO 交换 | `ZlgEtherCat.ConfigureExchange` | 固定：`ConfigureExchange` |
| 使能过程映像交换 | `ZlgEtherCat.EnablePi` | 固定：`EnablePi` |
| 禁用过程映像交换 | `ZlgEtherCat.DisablePi` | 固定：`DisablePi` |
| 读取交换状态 | `ZlgEtherCat.ReadExchangeStatus` | 固定：`ReadExchangeStatus` |
| 启动 PDO 后台采集 | `ZlgEtherCat.StartAcquisition` | 固定：`StartAcquisition` |
| 停止 PDO 后台采集 | `ZlgEtherCat.StopAcquisition` | 固定：`StopAcquisition` |
| 读取采集状态 | `ZlgEtherCat.ReadAcquisitionStatus` | 固定：`ReadAcquisitionStatus` |
| 读取历史采样 | `ZlgEtherCat.ReadHistory` | 固定：`ReadHistory` |
| PDO 数据读取 | `ZlgEtherCat.PdoRead` | 下拉：`ReadPdo`、`ReadPdoBits` |
| 等待新 PDO 样本 | `ZlgEtherCat.WaitNextPdo` | 固定：`WaitNextPdo` |
| PDO 数据写入 | `ZlgEtherCat.PdoWrite` | 下拉：`WritePdo`、`WritePdoMasked` |
| 启动 PDO 周期输出 | `ZlgEtherCat.StartCyclicOutput` | 固定：`StartCyclicOutput` |
| 停止 PDO 周期输出 | `ZlgEtherCat.StopCyclicOutput` | 固定：`StopCyclicOutput` |
| 创建周期读取池 | `ZlgEtherCat.PeriodicPoolCreate` | 固定：`PeriodicPoolCreate` |
| 添加周期读取项 | `ZlgEtherCat.PeriodicPoolAddItem` | 固定：`PeriodicPoolAddItem` |
| 更新周期读取项 | `ZlgEtherCat.PeriodicPoolUpdateItem` | 固定：`PeriodicPoolUpdateItem` |
| 停止周期读取项 | `ZlgEtherCat.PeriodicPoolStopItem` | 固定：`PeriodicPoolStopItem` |
| 读取周期读取池状态 | `ZlgEtherCat.PeriodicPoolStatus` | 固定：`PeriodicPoolStatus` |
| 关闭周期读取池 | `ZlgEtherCat.PeriodicPoolClose` | 固定：`PeriodicPoolClose` |
| 创建常驻读取池 | `ZlgEtherCat.BackgroundPoolCreate` | 固定：`BackgroundPoolCreate` |
| 添加常驻读取项 | `ZlgEtherCat.BackgroundPoolAddItem` | 固定：`BackgroundPoolAddItem` |
| 更新常驻读取项 | `ZlgEtherCat.BackgroundPoolUpdateItem` | 固定：`BackgroundPoolUpdateItem` |
| 停止常驻读取项 | `ZlgEtherCat.BackgroundPoolStopItem` | 固定：`BackgroundPoolStopItem` |
| 读取常驻读取池状态 | `ZlgEtherCat.BackgroundPoolStatus` | 固定：`BackgroundPoolStatus` |
| 关闭常驻读取池 | `ZlgEtherCat.BackgroundPoolClose` | 固定：`BackgroundPoolClose` |
| 读取 SDO 参数 | `ZlgEtherCat.ReadSdo` | 固定：`ReadSdo` |
| 写入 SDO 参数 | `ZlgEtherCat.WriteSdo` | 固定：`WriteSdo` |
| 读取数字量输入 | `ZlgEtherCat.ReadDigitalInput` | 固定：`ReadDigitalInput` |
| 写入数字量输出 | `ZlgEtherCat.WriteDigitalOutput` | 固定：`WriteDigitalOutput` |
| 读取模拟量输入 | `ZlgEtherCat.ReadAnalogInput` | 固定：`ReadAnalogInput` |
| 写入模拟量输出 | `ZlgEtherCat.WriteAnalogOutput` | 固定：`WriteAnalogOutput` |
| 枚举从站 | `ZlgEtherCat.EnumerateSlaves` | 固定：`EnumerateSlaves` |
| 从站信息与状态读取 | `ZlgEtherCat.SlaveDiagnostics` | 下拉：`ReadSlaveInfo`、`ReadSlaveStatus` |
| 确认从站状态错误 | `ZlgEtherCat.ClearSlaveError` | 固定：`ClearSlaveError` |
| 配置自动清错 | `ZlgEtherCat.ConfigureAutoClear` | 固定：`ConfigureAutoClear` |
| 配置就绪丢失超时 | `ZlgEtherCat.ConfigureReadyLost` | 固定：`ConfigureReadyLost` |
| 配置看门狗输出 | `ZlgEtherCat.ConfigureWatchdog` | 固定：`ConfigureWatchdog` |
| 读取看门狗配置 | `ZlgEtherCat.ReadWatchdog` | 固定：`ReadWatchdog` |
| 启用 PI 看门狗 | `ZlgEtherCat.EnableWatchdog` | 固定：`EnableWatchdog` |
| 禁用 PI 看门狗 | `ZlgEtherCat.DisableWatchdog` | 固定：`DisableWatchdog` |
| 手动喂狗 | `ZlgEtherCat.FeedWatchdog` | 固定：`FeedWatchdog` |
| 设置强制输出 | `ZlgEtherCat.ConfigureForceOutput` | 固定：`ConfigureForceOutput` |
| 读取强制输出 | `ZlgEtherCat.ReadForceOutput` | 固定：`ReadForceOutput` |
| 解除强制输出 | `ZlgEtherCat.DisableForceOutput` | 固定：`DisableForceOutput` |
| 启动 SDK 抓包 | `ZlgEtherCat.StartCapture` | 固定：`StartCapture` |
| 读取 SDK 抓包 | `ZlgEtherCat.ReadCapture` | 固定：`ReadCapture` |
| 停止 SDK 抓包 | `ZlgEtherCat.StopCapture` | 固定：`StopCapture` |
| 板载数字量读取 | `ZlgEtherCat.BoardDigitalRead` | 下拉：`ReadBoardDi`、`ReadBoardDo` |
| 写入板载 DO | `ZlgEtherCat.WriteBoardDo` | 固定：`WriteBoardDo` |
| 启动板载 PWM | `ZlgEtherCat.StartBoardPwm` | 固定：`StartBoardPwm` |
| 停止板载 PWM | `ZlgEtherCat.StopBoardPwm` | 固定：`StopBoardPwm` |
| 读取板载 PWM 配置 | `ZlgEtherCat.ReadBoardPwm` | 固定：`ReadBoardPwm` |
| 配置板载脉冲 | `ZlgEtherCat.ConfigureBoardPulse` | 固定：`ConfigureBoardPulse` |
| 输出板载脉冲 | `ZlgEtherCat.OutputBoardPulse` | 固定：`OutputBoardPulse` |
| 停止板载脉冲 | `ZlgEtherCat.StopBoardPulse` | 固定：`StopBoardPulse` |
| 读取板载脉冲配置 | `ZlgEtherCat.ReadBoardPulse` | 固定：`ReadBoardPulse` |
| 配置板载编码器锁存 | `ZlgEtherCat.ConfigureBoardLatch` | 固定：`ConfigureBoardLatch` |
| 启动板载锁存 | `ZlgEtherCat.StartBoardLatch` | 固定：`StartBoardLatch` |
| 停止板载锁存 | `ZlgEtherCat.StopBoardLatch` | 固定：`StopBoardLatch` |
| 读取板载锁存 | `ZlgEtherCat.ReadBoardLatch` | 固定：`ReadBoardLatch` |
| 复位锁存计数器 | `ZlgEtherCat.ResetBoardLatchCounter` | 固定：`ResetBoardLatchCounter` |
| FoE 读取文件 | `ZlgEtherCat.FoeRead` | 固定：`FoeRead` |
| FoE 写入文件 | `ZlgEtherCat.FoeWrite` | 固定：`FoeWrite` |
| 读取 ESC 内存 | `ZlgEtherCat.ReadSlaveMemory` | 固定：`ReadSlaveMemory` |
| 写入 ESC 内存 | `ZlgEtherCat.WriteSlaveMemory` | 固定：`WriteSlaveMemory` |
| 读取从站 EEPROM | `ZlgEtherCat.ReadSlaveEeprom` | 固定：`ReadSlaveEeprom` |
| 写入从站 EEPROM | `ZlgEtherCat.WriteSlaveEeprom` | 固定：`WriteSlaveEeprom` |
| 复位主站通道 | `ZlgEtherCat.ResetChannel` | 固定：`ResetChannel` |
| 复位板卡 | `ZlgEtherCat.ResetBoard` | 固定：`ResetBoard` |
| 读取板卡文件 | `ZlgEtherCat.BoardFileRead` | 固定：`BoardFileRead` |
| 写入板卡文件 | `ZlgEtherCat.BoardFileWrite` | 固定：`BoardFileWrite` |
| 更新板卡文件 | `ZlgEtherCat.BoardUpdate` | 固定：`BoardUpdate` |
| 绑定轴 | `ZlgEtherCat.BindAxis` | 固定：`BindAxis` |
| 解绑轴 | `ZlgEtherCat.UnbindAxis` | 固定：`UnbindAxis` |
| 轴状态与反馈读取 | `ZlgEtherCat.AxisRead` | 下拉：`ReadAxisState`、`ReadAxisMode`、`ReadAxisFeedback`、`ReadAxisDiagnostics` |
| 复位轴故障 | `ZlgEtherCat.ResetAxisFault` | 固定：`ResetAxisFault` |
| 设置运行模式 | `ZlgEtherCat.SetAxisMode` | 固定：`SetAxisMode` |
| 伺服使能 | `ZlgEtherCat.EnableAxis` | 固定：`EnableAxis` |
| 伺服去使能 | `ZlgEtherCat.DisableAxis` | 固定：`DisableAxis` |
| 位置运动 | `ZlgEtherCat.MovePosition` | 固定：`MovePosition` |
| 速度运动 | `ZlgEtherCat.RunVelocity` | 固定：`RunVelocity` |
| 转矩控制 | `ZlgEtherCat.RunTorque` | 固定：`RunTorque` |
| 回零 | `ZlgEtherCat.HomeAxis` | 固定：`HomeAxis` |
| 停止运动 | `ZlgEtherCat.StopMotion` | 固定：`StopMotion` |
| 同步运动目标 | `ZlgEtherCat.CyclicTarget` | 下拉：`WriteCspTarget`、`WriteCsvTarget`、`WriteCstTarget` |

开发手册 §18.1/§18.3 原要求每个动作独立步骤。仅七个高度相似操作组按用户明确要求采用下拉合并，作为这两条的显式例外；其它步骤保持单独操作；设置、独立编辑页、表达式、保存、校验与编译发布规范继续遵循手册。
