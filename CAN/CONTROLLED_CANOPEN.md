# CANopen 周期通信服务（1.0.49）

本文件说明当前版本的通信行为。软件假驱动验证不等于真实总线周期、物理停稳或完整平台运行验收。


## 同一连接混用（1.0.48）

CAN_Open只打开一次，各步骤使用同一ConnectionName（例如表达式"CAN1"）。CAN/CANopen/CiA402可以补充彼此未覆盖的功能，不按插件类别拒绝通信。

- 唯一底层接收泵：普通CAN_Read读取独立有界原始副本（保留FD/扩展帧），CANopen协议读取其FIFO，每个轴管理租约有独立TPDO inbox。快照不消费上述队列，慢观察者不阻塞协议。各独立inbox上限4096，满时淘汰最旧，GetReceiveDiagnostics分别记录；不改变设备时间或刷新旧帧龄。
- 普通CAN/PDO/SYNC可使用周期ID并进入同一FIFO，不能插入已准入整批；分开的PdoSend/SyncSend仍不是自动原子批次。周期源之间的RPDO/历史ID冲突校验保留。通用周期任务可与普通周期源共存，严格诊断只与其它后台源互斥，不能宣称独占外部发送或普通前台写。
- 轴管理租约与周期sender租约可共存、精确释放；同节点重复轴管理绑定仍拒绝。普通SDO可读写绑定节点的任意合法对象，完整事务继续串行，不绕过协议校验、取消和实际未退调用隔离。
- 所有标准SDO请求提交（包含原始Write和批次入口）先取得当前事务门，再排发送FIFO。已持真实活跃事务身份的内部SDO发送可绕重入，过期身份不能提交。
- 用户已确认“SDO用CANopen步骤，CAN补充其它报文”：对象读写使用通用CANopen SdoRead/SdoWrite，普通CAN用于PDO、SYNC及其它自定义报文。原始CAN_Write/CAN_Read保持独立步骤，完整手工SDO并发归属不在本轮范围；现有额外保护不将分离的原始写/读推断成完整SDO会话。
- 外部模式、映射和目标更改由序列协调；改变绑定时缓存的PDO映射后重新绑定。协议串行不等于两个独立运动控制算法自动协调。Unbind可按配置下发停机/释放使能，其设备动作会影响共享物理轴，序列需要安排目标发送与解绑顺序。
- 解绑/ServiceStop只释放自身资源，不关父CAN。CAN_Close可直接冻结并等待关联工作后执行合法SDK关断，无先解绑/停机ACK前置条件。真实原生未退或SDK失败仍保留句柄。
- 共享托管读取消仅结束该等待；连接隔离、关闭及超时围栏仍检查。模式转换先冻结新原始读取，等旧actual退出后再起唯一泵；等待含在配置预算，不能用软超时启动第二读者。UDS/XCP协议混用不属于本版新增范围。

## 当前：多个源、整批 FIFO、仅提交

- 每个 `ServiceName` 是独立资源。ServiceStart 默认仅接收；显式 EnableMonitoringSync 才发送周期 SYNC。允许多个不同名源共享 SYNC ID，各自配置/周期/生命周期，不限制两个。
- 队列项为完整 `[RPDO1,...,SYNC]` 或 `[SYNC]`。同一 FIFO 租约覆盖全部成员，不按成员 ID 排序、不插入另一源。最多等待 1024 个批次；满队列报错。按单调入队先后准入，完全同一时间戳才比较批次首帧 ID。这不约束外部应用或其它总线节点。
- 背景 MonitoringPeriodMs 与目标 PeriodMs 独立。例如背景 5 ms、控制 10 ms，RPDO 后额外 SYNC 不合并、不重置背景周期；数值由步骤配置。背景从自身批次起始计时，不按设备时间戳累计。纯监测迟到默认跳拍重同步，连续 3 次故障，可配置。Windows 不保证硬实时。
- RequireOnBusConfirmation 默认 false，旧 v1 设置显式迁移为 v2/false，其它值保留；v2 可显式保存诊断 true。NI 正常周期一次 AsSubmitted 整批写入，SDK 成功返回即结束提交，不等待 echo、不查询 session、不要求 echo 能力。原生调用失败/未退出仍保留真实错误与归属。
- 其它适配器在同一个整批租约内串行现有 Write，不能被本插件其它源拆散；未新增其原生整批 API，SDK 自身线上排队仍需实卡验收。ZLG 已有回显/时钟观察及其队列上限保持，未取得本版实卡持续负载证据。
- 显式 true 为独占逐批诊断，不与其它源混跑；同源背景/目标模式一致。模式预检先占租约，被拒绝启动不查询/改变运行中 NI 源；通用周期任务也不能在预检期间抢先启动。
- CycleTimeoutMs 约束排队/SDK 调用总期限，与 PeriodMs 分开。仅提交批次略超周期不再触发旧确认耗时判据；真正总期限超时仍 Error，不暂停计时或隐藏 SDK 失败。

## 当前：读取、时间与有限轨迹

发送、PDO 反馈和快照独立，共用接收路由；SDO 仍等待协议响应。普通SDO读写可补充轴/周期操作，完整事务仍串行，管理租约独立。同节点事务门与未知调用隔离保持。

反馈列表可配置 0~128 路，非纯 SYNC 服务至少一路，不固定四路。SnapshotRead 不等齐、不消费 FIFO。每路保留设备时间/时域/ClockId/采集下界；时钟未知或过期保持 TimeUnknown，不能把出队时刻当采集时刻。Receive.Running 仅指接收侧，ServiceHealthy/BackgroundError 汇总健康。共享 FIFO、最新快照替换和厂商队列统计口径独立。

| 点表设置 | 默认 | 行为 |
| --- | --- | --- |
| MissPolicy | SkipAndCatchUp | 只发最新到期点，跳过中间点，不密集补发 |
| LatenessWindowMs | 0 | 0 使用 PeriodMs；超窗不提交过期点 |
| MaxSkippedPoints | 20 | 累计跳点上限，提交前检查 |
| HoldTimeoutMs | 100 | HoldLast 最长保持，不延长总时长 |

Fail 保留原严格错点失败；HoldLast 有限重复上次已提交原始字节，必须提供设备审查的停机 SDO，不等于物理位置保持。初点不可跳过，终点须真实提交，期限外不强发。裸字节策略无法证明速度/加速度连续性，须序列规划层评估。

首点时钟锚在首批 FIFO 准入；后续每次准入后按当前单调时间重选并检查，不按排队前的旧选择直接发出。完整提交后才记 ExecutedTrajectoryPoints/SkippedTrajectoryPoints/HeldTrajectoryCycles。TerminalSubmitted 与 TerminalConfirmed 分开；点表完成不代表物理到位。后台只使用复制点表，不访问活动表达式。

公共 CycleOptions/MonitoringOptions 既有构造默认值为兼容旧调用保留；步骤始终传入显式 v2 配置。独立 API 调用方应显式指定仅提交模式及点表策略。

## 当前：停止、关闭、证据

1. CycleStop 捕获运动代次，冻结目标并撤销尚未准入的整批。已准入 RPDO→SYNC 在自己的期限内完整提交，再执行可选停机 SDO；不因下一监测批计数变化误判旧运动。其它源/接收保留。
2. StopCommandsAcknowledged 仅在全部停机 SDO 应答成功后设置；MotorStoppedVerified 仍 false，停稳由序列核验。
3. SubmissionCompletedFrames 与 ConfirmedFrames 分账，正常仅提交 ConfirmedFrames 保持 0。HostSubmissionQuiescent 只证明本源主机调用退出；OwnedTxQuiescent/ServiceShutdownConfirmed 保留严格确认证据，不伪造 true。
4. ServiceCleanupCompleted 表示本源订阅/占用/任务/回调退出且无部分批次，正常仅提交 ServiceStop 可据此完成，历史故障保留 JSON/Warn。PartialCycle、未退调用及关闭错误仍 Error。
5. CAN_Close 独立冻结全部源、撤销排队批次，等待真实 I/O/回调/任务退出并检查 SDK Stop/Close/Clear，不要求此前 ServiceStop 或停机 ACK。合法释放后允许重开；真正原生/SDK 释放未完成保留句柄。超时不等于撤回报文，不自动重连/重发或清未知。

HostPathMs 为旧 AdapterBatchMs 的兼容别名，均是主机墙钟路径，不是纯线缆时间。正常 NI 路径 SessionCompletionQueries=0；DTO 出现字段本身不能证明执行查询。实际时序/接受范围和退出证据见 Timing/Batch。

CAN核心/UI均1.0.49，配套CiA402核心/UI为1.0.14，既有步骤身份保持。本地完整目录为 artifacts/Release/2026-10-27-final-plugins/Plugins/CAN；编译验证时未部署，源码提交状态以 Git 为准。软件证据见 ../docs/PLUGIN_CHANGES.md。

更新/回退须关闭平台并确认原生工作退出，成套替换 CAN 核心/UI/依赖；CiA402 私有 CAN 副本同步同版。旧候选包已清理；若需回退，请使用自行保留的完整旧版包及配套序列，并核对确认模式和设置兼容性。真实 NI/ZLG 时序、BusOff、断线/重开及电机停稳尚未验收。
