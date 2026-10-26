# 受控 CANopen 通信服务（1.0.33）

本次提供通信层软件实现与模拟验证，不是实卡运动控制合格报告。COB-ID、DLC、字节顺序和目标值全部由序列配置，插件不推断位置/速度/转矩，不修改设备 PDO 映射、NMT、使能或安全互锁。

## 可用范围与硬件验证边界

- 连续接收、按 ID 的共享 FIFO、非消费式最新快照，以及后台任务退出管理已实现。
- 单工作任务依次提交全部 RPDO，再提交 SYNC；失败不继续发送本周期的剩余帧，不补发迟到周期。软件定时不是硬实时。
- NI 已实现 `ICanConfirmedTransmitter`：写前登记，连接代次/顺序/完整帧/时间屏障匹配回显，同时要求原生 Write 成功退出及本发送会话 `nxWait(TransmitComplete)` 成功。回显属于整个接口，不能单独证明本次发送；不把所匹配回显的时间戳公开为本帧精确发送时间。
- ZLG USBCANFD-100U/200U（包括其 Classic 报文）在驱动接受回显配置后，支持回显加原生成功退出的严格确认。其他型号仍支持原有普通 CAN 收发和共享采集，但未经核对的严格确认能力会拒绝启动；不能把 Transmit 返回数量解释为总线完成。
- 取消勾选确认选项只可用于隔离实验中的主机提交顺序验证，不证明实际总线顺序。此模式没有正常停止的发送完成证据，CycleStop/ServiceStop 可能保持隔离；父 CAN_Close 仍独立执行合法 SDK 终态关断，但不证明历史报文撤回或电机停稳。
- NI 保留 RawTimestamp100ns，使用读取接口时间前后的 Stopwatch 区间建立保守映射。帧使用不晚于设备采集时间的校准点的 host-before 作下界，不插值、不假定振荡器漂移精度。每批接收后校准，空闲时约 5 ms 校准；缓存最多 4096 个点，超过 250 ms 未校准、检测到跳变、旧帧早于映射历史时为 TimeUnknown。ClockId 在重置时更新；缓存快照也检查当前时钟有效性。不能用出队时刻刷新帧龄。
- ZLG 保留 RawTimestampUs；已确认自身发送回显的时间戳与提交前 Stopwatch 时刻形成因果下界。只有设备时间不早于有效锚点的帧可使用该下界，不外推、不以出队时刻刷新。无锚点、旧帧、超过 250 ms 未更新、异常那次校准均保持 TimeUnknown；时钟代次重置使旧快照无效。纯被动接收尚无可信时钟锚点，显式监测 SYNC 的自身回显可持续建立锚点。其他适配器仍保持未知时间。
- 真实 NI/ZLG 等卡上的发送完成、时间域/时钟重启、队列溢出、断线退出、总线顺序及现场设备安全均未验证。完成厂商能力适配和实卡测试前，不应宣称本次有序目标发送可用于生产运动控制。

## 新增步骤与配置

| StepType | 用途 | 主要配置 |
| --- | --- | --- |
| `CANopen.ServiceStart` | 默认仅接收；可显式启用纯 SYNC 监测 | ConnectionName、ServiceName、反馈列表、EnableMonitoringSync、MonitoringPeriodMs、SYNC |
| `CANopen.CycleStart` | 启动单执行单元的 RPDO→SYNC | NodeIds、RPDO 数据列表、SYNC、PeriodMs、CycleTimeoutMs、MaxLatenessMs、InputMaxAgeMs、RequireOnBusConfirmation |
| `CANopen.CycleInputsUpdate` | 原子发布下一版目标字节 | 同一服务、同数量/顺序/COB-ID/DLC 的完整 RPDO 列表 |
| `CANopen.SnapshotRead` | 立即读取配置列表的最新快照及任务状态，不阻塞等齐帧 | ResultVariable（已声明 string）、RequireAllValid |
| `CANopen.CycleStop` | 冻结目标，确认发送收尾，执行已配置停机 SDO，保留采集 | StopTimeoutMs |
| `CANopen.ServiceStop` | 停止目标并释放该服务的观察订阅 | StopTimeoutMs |

六个步骤各有编辑器。反馈和目标列表支持添加/删除，禁用列排序和拖动重排，避免改变发送顺序；编辑值立即保存。表达式框接受宿主表达式，当前编辑器为普通文本输入，不提供表达式弹窗/自动补全。

示例连接名填写 `"CAN1"`，服务名填写 `"Bench"`，节点列表字符串表达式填写 `"1,2"`。反馈列表支持 1~128 路，每路名称、COB-ID、DLC、有效期按序列配置，不固定四路或固定 TPDO1；名称/ID 必须唯一。SYNC 的空字符串数据表达式 `""` 表示 DLC=0；按原设备协议选择 DLC=0 或 1。

序列在自己的步骤上下文中计算目标并组包，再执行 CycleInputsUpdate 发布完整字节快照；后台不持有活跃序列上下文做表达式求值。更新失败不会发布半份目标；超过 InputMaxAgeMs 未更新会故障停止，不无限重复旧目标。

## 快照语义

ResultVariable 是完整 JSON string；不自动创建变量。RequireAllValid 默认关闭，允许观察缺失、过期和未知质量数据；这不意味着数据有效。开启后任一路无效则步骤 Error。后台故障无论此选项如何均返回 Error。

- 每路：DataHex、COB-ID、实际/期望 DLC、Sequence、SourceTimestampNs、TimeDomain、ClockId、IngressMonotonicTicks、AgeUpperBoundMs、FreshnessKnown、Quality/Valid。
- Sequence 是每个配置反馈在本次采集订阅中的独立到达序号，不是设备报文计数器。各路快照不要求来自同一 SYNC 周期。
- Quality 区分 NeverReceived、LengthMismatch、TimeUnknown、Stale、OutOfOrder、ClockReset、SourceFault、Stopped；只有可信时间且未过期、长度正确时 Valid=true。
- PendingFrames/FifoEvictions 是共享 FIFO 深度及每 ID 64 帧满队列淘汰累计；LatestReplacements 只是最新值覆盖次数，不等于丢帧。厂商未提供的队列深度/丢帧数为 null，不能按 0 解释。
- Cycle 提供状态、代次、输入版本、完整周期数、提交数/确认数、耗时、超限数、失败阶段、原始错误、HostExited、OwnedTxQuiescent。

快照观察不消耗 FIFO，因此并行读快照不会抢走 SDO 应答。SDO 保持已有事务/节点所有权和协议校验；事务开始清理已缓存的旧应答，未完成事务继续隔离，不能靠重试绕过安全保护。

## 启停与兼容性

1. CAN_Open 建立连接；同名连接拒绝替换，同一解析后物理通道不能用不同逻辑名重复打开。NI 配置工具中的外部设备别名不由本插件猜测映射。
   本插件的占用锁不约束外部应用或总线上的其他主站。运动前必须确认没有其他 SYNC/目标生产者；本实现不将 NI 接口切换为 ReplayExclusive，也不把主机锁等同于总线独占。
2. ServiceStart 将该连接切换到受控 CANopen 模式。不能有进行中的原始 I/O/通用周期任务，不能已用于 UDS/XCP。
3. 该连接中的原有 CANopen SDO/PDO/心跳/EMCY 步骤通过同一共享入口接收。CAN_Read、UDS、XCP 原始读取拒绝混用；须关闭后重新打开才能恢复原始模式。
4. 控制发送保留 NodeIds 所有权及 RPDO/SYNC ID；与同节点 CiA402 控制占用冲突时拒绝启动，不接管轴。纯采集服务不占用节点控制权。
5. CycleStop 不代表电机停止。仅接收模式下退出目标工作；显式监测模式下，完整周期及已配置停机 SDO 全部应答后回到 Monitoring，后台调度者和 SYNC 继续运行。监测模式启动运动必须先提供审查过的停机配置。StopCommandsAcknowledged 只代表 SDO ACK，MotorStoppedVerified 固定 false；停稳须由序列核验。失败或部分周期不自动恢复监测。
   CycleStop 捕获本次运动代次与固定停止结果，下一监测帧尚未确认不使旧运动停止失败。后续监测故障仍在快照 Error 中锁存；旧等待遇到新运动会拒绝旧代次，不改变新运动占用。Monitoring 下 HostExited=false 是后台调度仍工作的预期状态。
6. 只有已确认完整周期才能释放目标所有权用于重新启动。部分 RPDO 已发送但 SYNC 未完成时进入 PartialCycle，不能直接重启或补发 SYNC。父 CAN_Close 冻结准入并等待真实任务/I/O退出后仍执行 SDK 关闭；设备可能持有旧 RPDO，必须由序列按设备规范恢复，不能把关闭当作电机停稳。
7. 超时、取消不等于原生调用退出。停止/关闭均有界等待；真实 I/O/回调/SDK 关闭未退出或失败时保留对应句柄。待发未知仍禁止恢复运动及正常 CycleStop/ServiceStop，但不应永久阻断合法 SDK 终态关闭。连接资源回收异步发起关断；正常序列应显式执行 CAN_Close 检查结果。
8. CAN_Close 独立关闭本连接，不要求此前 ServiceStop 成功或停机命令 ACK。真实任务退出后 SDK 关闭成功、普通后台也退出时步骤返回 Completed；历史故障保留 Warn 与 Value，内部 CloseAsync 仍保留历史异常兼容。物理已释放而普通日志/表达式未退出时继续按剩余总预算等，超时明确 Error，但不能继续占用已释放硬件；旧 worker 保留并冻结。SDK 未实际释放时不得报告成功或强清句柄。

原通用 CAN_Cyclic_SendStart/Stop 的配置与逐报文调度方式保留，但任务改为连接内具名、重复启动报错、停止等待实际任务退出；它仍不提供 RPDO→SYNC 顺序保证。

## 部署和回退

CAN 与 CAN.UI 当前版本均为 1.0.40；配套 CiA402 为 1.0.12，私有 CAN 必须与主插件一致。关闭测试平台后备份并成套替换完整插件目录，不要仅替换一个 DLL。宿主需符合现有 SDK 1.2.8 契约；实际宿主加载与私有依赖解析仍需集成验证，不能混用来源版本。目标机需安装匹配硬件的 NI-XNET 或 ZLG Windows 驱动；随包 ZLG 用户态 DLL 不替代板卡驱动。

回退时关闭平台，恢复备份的完整 CAN 文件夹，并停用新增六种步骤；旧版本不认识这些 StepType。既有 CAN/UDS/XCP 设置格式保持不变，但同名覆盖和停止行为变得更严格。新功能仍采用原 RPDO/SYNC 标准帧，不增加厂商私有报文。

具体软件测试、审查与未验证项见 `docs/plans/2026-10-23-ni-confirmed-sync-trajectory.md`。

## 点表、监督与停机输入

ServiceStart 默认 EnableMonitoringSync=false 保持旧语义。若设备采用原工程协议，显式配置 SyncCobId=`0x80`、SyncData=`"00"`（DLC1）；反馈数量及各路布局按当前序列填写。监测周期与 CycleStart 的 PeriodMs 分开；同一连接只允许一个调度者，目标发送时先逐帧确认 RPDO，再确认关联 SYNC。

实时目标采用 Targets + CycleInputsUpdate，InputMaxAgeMs 是续期限制；CSV/CST 可重复发布恒定字节。有限 CSP 点表选择 UseFiniteTrajectory，TrajectoryJson 填预先声明的 JSON 字符串变量（例如 `Locals.PointsJson`），TrajectoryDurationMs 单独填写。示意数据：

```json
[
  {"OffsetMs":0,"Frames":[{"CobId":513,"DataHex":"01 00 00 00"}]},
  {"OffsetMs":25,"Frames":[{"CobId":513,"DataHex":"02 00 00 00"}]}
]
```

上述只是原始字节格式示例，不是任何电机的有效目标。时间从 0 开始、严格递增，DurationMs 必须大于最后偏移；每点帧的数量/顺序/ID/DLC 必须相同。ID/索引在 JSON 内用十进制整数，DataHex 不换字节序。逐点按单调时间绝对偏移调度；超过 MaxLatenessMs、错过下一点或结束时尚有未执行点均故障，不突发补发。点表已完整提交，不套用 InputMaxAgeMs；不支持边跑边追加分块。本版后台不做单位换算、轨迹规划或插值。点表最多 100000 点，JSON 输入上限 8000000 字符。

StopCommandsJson 是字符串表达式，内容为数组，每项字段 `NodeId`（1~127）、`Index`（0~65535）、`SubIndex`（0~255）、`DataHex`（1~4字节）。默认 `[]` 不发送设备停机字。用户必须从设备审查配置提供内容；最多 32 条，按顺序使用当前节点 owner 的共享 SDO 通路执行，总期限 StopTimeoutMs。配置与运动启动原子提交，失败的并行启动不会覆盖成功启动的停机命令。

SuperviseFeedback 默认 false 兼容旧设置；运动建议显式开启，FeedbackStartupGraceMs 后每路必须可信且未过期。不要求来自同一 SYNC。实时目标断供、反馈失效或周期故障冻结新目标；只有 I/O 已退出、没有 PartialCycle/未知发送时才尝试配置的停机命令。停机失败不自动重试；后台 RX 故障时保留诊断及隔离，不能另开读者绕过路由。

NI 严格确认等待超时/取消后保留 ticket，并禁止新写入；迟到证据不自动恢复目标。父 CAN_Close 不等缺失回显作为前置：真实 host/pump 已退出后调用 SDK Stop/Clear，Clear 成功才释放会话；未决事实和原服务 confirmed 计数保持。调用方停止等待、发送完成、SDK 关闭及电机停稳分别判定。普通 NI Write 未切换严格模式时不强制依赖 TimeCurrent/nxWait；切换前仍需确认之前发送完成，预检有配置期限，真实 native 未退出保留占用。

## NI 声明依据与验收限制

本机未安装 SDK。使用 [NI 官方公开头文件固定提交](https://github.com/ni/grpc-device/blob/94ce92e0f1e869e2d85aa82906d7111417dc8952/imports/include/nixnet.h)：`__cdecl`；session/u32 为 4 字节；TimeCurrent `0x07130001` 返回 u64 FILETIME（1601 UTC 起100ns、8字节）；SessionInfo `0x00130004` 为4字节且低2位为状态；TransmitComplete 条件 `0x8001`。未复用不同结构的 TimeCurrent2。

依据 [NI Echo 属性](https://github.com/ni/grpc-device/wiki/NI-XNET-XNET-Session-Interface-Properties#interfaceecho-transmit) 与 [NI nxWait/nxReadState](https://github.com/ni/grpc-device/wiki/NI-XNET-General-Functions#nxwait)。这些是源码/官方契约核对，不是本机驱动 ABI 实测。部署机仍需验证所安装驱动、卡型号、真实 RPDO→SYNC 顺序、周期抖动、时钟重置、拔线/BusOff、停止及设备安全互锁。

## ZLG 声明依据与限制

依据随插件官方二开文件 `Native/Zlg/x64/canframe.h:60` 的 `TX_ECHO_FLAG=0x20`（Classic.__pad / FD.flags，发送至总线后回显），`zlgcan.h` 的接收微秒时间戳和 `usbcanfd-100u.xml` / `usbcanfd-200u.xml` 的 `set_device_tx_echo`。厂商[回显功能说明](https://www.zlg.cn/index/video/vclass/id/2/p/3.html)也明确回显能力按型号区分；不能由 USB/PCIe 或“支持 CAN FD”推断所有型号具有相同能力。

单个接收泵批量读取两类队列；回显按本连接代次、登记顺序、ID、帧格式、实际补齐 DLC/数据和设备时间屏障关联。Classic/FD 的队列出队顺序不同不丢弃可归属回显；重复设备时间不能确认下一份相同目标。Open 检查 ClearBuffer，重置全部代次边界；旧接收泵退出前不重开。等待取消后仍跟踪票据，不自动恢复运动；SDK CloseDevice 成功后以未知状态退休旧票据，不补写 ACK，不让旧代次影响新 Open。

普通 ZLG 写入在支持回显的通道上也跟踪未决项，旧项未确认前不能切入严格模式或销毁句柄。设备拒绝回显配置时普通写入继续可用，但严格启动在发送目标前报 UnsupportedGuarantee。无回显的驱动不提供严格运动启停证明；不要通过关闭确认绕过该限制。

插件占用不约束外部进程：使用严格确认时要求本插件独占该物理通道的发送，禁止外部应用/定时任务产生同 ID/同数据回显或额外 SYNC。此版软件调度不提供硬件定时回放。未进行实卡 ABI、固件回显、微秒时钟复位、周期和停止验证。

## 序列适配判据

- Error 仍需失败锁存，不能通过忽略 Error、修改计数或关闭确认规避。
- CSP 点表执行结束只表示点表发送收尾，不证明到位。
- 如需连续三次零速，序列应使用指定反馈路由的三个不同 Sequence，且每次 Valid/FreshnessKnown 为真、ClockId/AcquisitionEpoch 一致；重复读同一快照不能累计。
- 停止后服务仍保留 RPDO/SYNC ID 占用，前台不能直接发送旧 RPDO；准备动作通过被允许的 SDO 事务完成。原始 0x6061 读取安排在周期节点占用前/后，运行期使用已配置 PDO 中的模式反馈或受控 owner 通路。停止 ACK 不代表物理停稳。

本轮验证记录见 `docs/plans/2026-10-23-motion-stop-zlg.md`。
