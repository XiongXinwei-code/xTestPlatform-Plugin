# CANopen 受控周期通信服务：修改方案与接口设计

日期：2026-10-23。任务等级：L3（共享接收、周期目标、原生 I/O 与异步退出）。

状态：2026-10-23 用户授权实施。以下设计保留原始依据；实施差异、实际验证与硬件限制另记于同目录实施记录。

## 1. 已确认的需求与边界

- 用户确认 TPDO/RPDO 都由序列配置。继续使用原有四路 TPDO 的 COB-ID、DLC、字段定义与小端组包，不在插件中固化设备对象、位置/速度/转矩字段或节点偏移。
- 本次只设计三项：带时间的最新反馈快照；单执行单元有序发送与状态查询；可确认退出的生命周期。
- 背景采集与目标发送分开。停止目标发送不释放采集、不隐式停轴/释能，不改 NMT/PDO 映射、不自动重连。
- 不将现有通用周期发送器的独立报文任务组合成 RPDO/SYNC 顺序保证。
- 不重写 CiA402 状态机，不修改平台引擎、SDK、项目运行规则；但 CAN 自身 Open/Close、接收路由和资源所有者需要接入新生命周期。这是落实要求的必要改动，不是只增加一个后台 Task。
- 本设计不提供硬实时或物理停轴保证。停止发送、驱动发送队列清空、ECU 不再执行旧目标是三个不同结论；最后一项需要设备协议、状态反馈与硬件安全链证明。

## 2. 核对依据

### 2.1 当前源码

- `CAN/CanPlugin/CANopen/CanopenConnectionAccess.cs`：按适配器共享；`_receiveGate` 覆盖整次等待；`_sdoGate` 保护事务，节点 TPDO 接收也持有它；其他 ID 存入 `_pending`，每 ID 64 帧、满后淘汰最旧。
- `CAN/CanPlugin/CANopen/Executors/CanopenExecutors.cs`：PdoReceive 等待一帧并输出十六进制，没有快照年龄契约；SdoRead 通过共享总线与事务门收发。
- `CAN/CanPlugin/Models/CanMessage.cs`：有 `TimestampNs`，没有显式时间域、时钟实例或新鲜度证明。
- `CAN/CanPlugin/Adapters/NiXnet/NiXnetAdapter.cs`：已有原生接收泵与有限内存队列；不能再加第二个 NI 原生接收者。其他适配器也须核对各自时间戳语义。
- `CAN/CanPlugin/Executors/CanCyclicSendStartExecutor.cs`：每条报文独立 Task/PeriodicTimer、直接调用适配器 Write；任务表只保存 CTS，键不含连接名。
- `CAN/CanPlugin/Executors/CanCyclicSendStopExecutor.cs`：只请求取消、不等待实际发送任务退出；ConnectionName 未用于取任务。
- `CAN/CanPlugin/Executors/CanCloseExecutor.cs`：目前直接关闭适配器，没有先等待关联周期任务。
- `CAN/CanPlugin/Executors/CanOpenExecutor.cs`：目前资源表 Set 可静默销毁同名旧适配器，需防止带活动任务的连接被绕过关闭屏障替换。

### 2.2 开发手册

已读取云端 master 开发手册的版本、资源表及后台错误规范，并与本地 Git blob 比较：均为 3.4.0，blob `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`。

参考：[云端插件开发手册](https://github.com/XiongXinwei-code/xTestPlatform-Plugin/blob/master/xTestPlatform_StepPlugin_Development_Guide.md)。

实现时使用 Execution 资源、TryAdd 与身份核对；Completed/Error/Aborted 遵循 SDK 1.2.8；后台故障由状态及有界过程日志上报，不事后改写启动步骤结果，不写入未配置的隐式变量。

## 3. 推荐架构

```text
CAN_Open -> CanConnectionResource（连接代次、关闭屏障、隔离状态）
                 |
                 +-- 唯一接收入口 / 共享 CANopen Router
                 |       +-- SDO 应答与事务等待者
                 |       +-- 旧 CANopen FIFO 接口
                 |       +-- TPDO 观察订阅 -> 不可变最新快照
                 |
                 +-- 统一发送仲裁
                         +-- 普通 CANopen/SDO 发送
                         +-- 周期执行单元：目标计算 -> RPDO 列表 -> SYNC

序列：发布输入、读取快照/状态、请求停止；不直接操作后台 Task/CTS。
```

### 3.1 连接与路由所有者

1. `CanConnectionResource` 实现原有 `ICanAdapter` 外观，保留已有资源键，内部独占真实厂商适配器。关联 runtime 负责收发、关闭和任务跟踪；不改变厂商驱动的所有权。
2. `CanopenConnectionAccess` 接入同一个 runtime，不再让每个协议等待者直接读取真实适配器。受控模式下后台路由持续接收，等待某个 ID 不应占住全连接接收门。
3. NI 原生读取仍由其既有接收泵独占；runtime 从 NI 托管队列取帧。其余厂商由统一入口读取。每层只有一个实际读取者，不复制另一条原始收包路径。
4. SDO 在发送请求前注册应答等待，匹配事务、节点及协议字段；`_sdoGate` 仍串行化 SDO 事务。背景 TPDO 观察不占用该门，也不消费 SDO 应答。
5. TPDO 快照是路由器的观察副本，不从 SDO/FIFO 的消费队列抢帧。不引入同一帧在两个事务间重复匹配；复制发布前保留原始接收元数据。
6. 旧 CANopen PdoReceive 仍保留 FIFO 语义，不静默变成“最新值读取”；新步骤显式读取快照。旧队列上限保持有界，并增加积压/淘汰计数。

从旧模式切入受控模式前，必须封锁新的原始读写，并等待已经进入的操作实际退出；仍未退出时保持隔离，不能直接启动另一条接收泵。退出受控模式同样按代次完成交接，不通过清空引用制造“已经没有读者”的假象。

### 3.2 混用限制与控制归属

- 每个物理连接首版只允许一个活动的有序周期执行单元；多个节点/RPDO 放入同一个有序批次，避免多个 SYNC 主者。只读快照订阅可多个。
- 发送开始前显式声明节点、RPDO COB-ID 和 SYNC COB-ID 的控制占用。与现有 CiA402 节点占用或其他写入者冲突时报错；不通过改服务名绕过。
- 普通 SDO 与周期批次共用发送仲裁。SDO 只在实际写请求期间占发送门，等待应答时不占该门；不允许其他本连接软件发送插入一个 RPDO/SYNC 批次。
- 受控模式内，原始 CAN_Read、UDS、XCP 若尚未接入同一接收分发契约，必须在任何协议发送前拒绝混用；不能任由它们直读真实队列，也不能仅在读失败后才报错。受控模式外保留旧行为。
- 原始 CAN_Write 与通用周期发送器不得写入本周期执行单元已占用的 RPDO/SYNC；冲突检查前置。其他外部应用/总线节点不受本插件控制，不能承诺全总线排他。
- 连接外观与 runtime 不能互相递归调用 Read：runtime 只持有私有原生适配器，公开外观只进行路由接入或冲突拒绝。

## 4. 快照与时间契约

### 4.1 每路反馈 `FeedbackRouteSnapshot`

| 字段 | 契约 |
| --- | --- |
| RouteName / CobId / ExpectedDlc / ActualDlc | 启动时从序列配置固定；不推导或重配设备 PDO。 |
| Data | 原始载荷的只读副本；保持 DLC 和字节序，不按厂商自动改包。 |
| ConnectionEpoch / AcquisitionEpoch | 本次物理连接与采集实例的唯一代次；重新连接/采集不能继承旧有效性。 |
| ReceiveSequence | 本路本代次递增的接收序号；只是本地序号，不冒充 ECU 线上帧计数或丢帧证明。 |
| SourceTimestamp / TimeDomain / ClockId | 保留来源时间值、单位/时间域和时钟实例；未知时明确 Unknown，不能默认当 UTC。 |
| IngressMonotonicTicks | 在可取得的最早托管入口记录，只表示本机观察到帧；沿后续队列传递时不重写。 |
| AgeUpperBoundMs / FreshnessKnown | 来源时间经可信时钟映射后的保守帧龄；不能确定时年龄为 null、FreshnessKnown=false。 |
| Quality / Valid / InvalidReason | NeverReceived、Valid、Stale、LengthMismatch、TimeUnknown、ClockReset、OutOfOrder、SourceFault、Stopped 等；Valid 必须有足够证据。 |

同一次查询返回最新不可变状态，不等待四帧齐全；每路缺失/无效单独标记。读取已有快照不推进 ReceiveSequence，不改变接收时间，也不恢复其 Valid。

最新到达帧长度错误时保留错误信息并使当前路无效，不悄悄保留上一帧 Valid。来源时间倒退/重置时不能用倒退帧覆盖有效性后继续当新样本使用。

### 4.2 防止“旧帧刷新有效期”

- 硬件/驱动采集时间与主机单调时间分开保存。将 `DateTime.Now` 或 `Stopwatch.GetTimestamp()` 写在出队处，只能获得观察时间，不是采样时间。
- 只有经过核实的采集时间域和带误差上界的时钟映射，才可计算采集年龄上界。不能用第一帧出队时间去标定未知积压帧，再把它证明为“刚采集”。
- 没有来源时间或可靠映射的适配器仍可输出原始数据和观察时间，但标记新鲜度未知；默认不能将其作为要求已知新鲜度的反馈。
- SDK 原始时间可能是绝对时间、设备计数或同步时钟；当前 `TimestampNs` 的乘法/换算并不足以证明这些时间同域。实现前逐厂商核对，不能跨连接直接减时间戳。
- 重连、时钟回拨、设备计数回绕/重启、时间戳缺失和映射过期均使旧映射失效。主机年龄计算使用单调时钟，不受墙钟调整影响。
- 同一次返回四路“最新快照”不等于来自同一 SYNC。增加 `CaptureSpreadMs`/`CoherenceKnown`；除非序列协议提供可验证的周期计数，不声称同周期采样。

### 4.3 状态与计数 `CommunicationSnapshot`

包含 SnapshotVersion、服务/发送状态、四路快照、每路格式错误/乱序计数，以及以下分层指标：

- 驱动队列积压与驱动溢出数：接口支持时提供；不支持时为 null 并注明 Unknown，不能写 0。
- 适配器托管队列深度、高水位与溢出数：如 NI 既有有限队列的淘汰数。
- 共享 CANopen FIFO 深度及每 ID 64 帧淘汰数。
- 快照覆盖/合并次数：最新值模式下正常的合并指标，不等同于传输丢帧。
- 首次后台故障、最近故障、阶段、异常链、连接/采集/发送代次、最后接收与最后成功周期。

不得把上述层级计数相加后称为“总线真实丢帧”。没有 ECU 线上序号时，无法证明线上的总丢帧数量。

## 5. 有序周期发送契约

### 5.1 配置与输入

- RPDO 列表保持序列配置顺序，配置包含 NodeIds、每帧 COB-ID、ExpectedDlc、原有数据组包表达式/目标计算定义。
- SYNC 的 COB-ID、无载荷/既有计数器格式全部显式配置，不改变原协议。周期、计算预算、最大起始延迟、总周期期限、通信期限明确区分。
- `CycleInputsUpdate` 在序列线程求值并一次发布不可变输入，附 InputVersion/有效期。后台不能同时从多个正在变化的变量拼出一份不一致的命令。
- 周期计算只接收固定的 CycleContext：CycleIndex、计划/实际开始时间、输入版本、当前不可变反馈。启动时固定求值绑定，不长期依赖活动 `IExecutionContext.CurrentStep` 或启动步骤的取消令牌。
- 现有字节组包在 TargetProgram 的表达式适配层保留；不得重新定义小端字段。不依赖未经核实的 SDK 后台求值线程安全；独立只读求值上下文的可行性是实现阶段前置验证项。
- 计算超时或未退出时，不能另起下一次计算。旧计算晚到结果通过发送代次检查丢弃，绝不转成晚到 RPDO。

### 5.2 单周期执行

```text
单调时钟到期
  -> 取得一份输入版本与反馈快照
  -> 计算该周期全部目标 / 校验 DLC
  -> 复核发送代次、停止请求与期限
  -> 获得有序批次发送权
  -> 按序 RPDO[0..N-1]
  -> 达到配置要求的发送完成级别
  -> SYNC
  -> 更新周期结果与延迟指标
```

- 计算、任一 RPDO 或发送确认失败时，不继续提交本周期 SYNC，不自动重发下一周期、不追赶补发旧周期。
- SYNC 失败时标记 PartialCycle/SyncFailed。ECU 可能已有未应用 RPDO，后续恢复不能简单继续发 SYNC；暂停该 SYNC 域并由序列按设备规范处理。
- 周期起始迟到/计算超限/总期限超限均有独立计数、失败阶段与实际耗时；首版默认停止进一步目标提交并保留采集，不自动改写零目标/停轴命令。
- Windows 软件调度不是硬实时；PeriodMs 可配置不等于某个 1 ms 周期已受支持。目标周期应经过板卡/驱动负载资格验证。

### 5.3 区分调用顺序与物理发送顺序

接口必须区分 HostSubmissionOrdered、DriverFifoOrdered、OnBusConfirmed 等保证级别。RPDO 的 Write 返回后才调用 SYNC，不能独自证明总线上的顺序；驱动队列/硬件发送优先级和重发可能影响结果。

首版受控发送默认要求经验证的 RPDO-before-SYNC 能力：采用有证据的 FIFO 发送契约，或等待 RPDO 发送完成后才允许 SYNC。适配器不支持时，启动返回 UnsupportedGuarantee，不静默降低为“只保证方法调用顺序”。不能对所有厂商预先宣称支持。

`ConnectionCapabilities` 分别声明 CaptureClockDomain、CaptureAgeMapping、DriverQueueStatistics、ManagedQueueStatistics、OrderedTxGuarantee、TxCompletion、TxQueueDrain、ScopedTxAbort、NativeCancellationBound。每项允许 Unsupported/Unknown，不用单一 SupportsCyclic 标志掩盖差异；共享发送队列没有按服务隔离取消能力时，不启用 ScopedTxAbort。

## 6. 核心接口草案

以下是待实现的契约，不是现有 SDK API，也不是本轮已编译的源码。

```csharp
public interface ICanopenCyclicService : IDisposable, IAsyncDisposable
{
    Task StartAcquisitionAsync(AcquisitionConfig config, CancellationToken ct);
    CommunicationSnapshot ReadSnapshot(); // 不等待帧、不做设备 I/O
    CycleStatus ReadCycleStatus();        // 当前软件状态的不可变快照
    Task StartTargetsAsync(TargetPlan plan, StartOptions options, CancellationToken ct);
    void PublishInputs(CycleInputs inputs); // 原子替换；输入版本递增
    Task<TargetStopResult> StopTargetsAsync(StopOptions options, CancellationToken ct);
    Task<ServiceCloseResult> CloseAsync(StopOptions options, CancellationToken ct);
    Task Completion { get; }              // 本服务全部实际工作已退出
}

public interface ICanConnectionRuntime
{
    Guid ConnectionEpoch { get; }
    ConnectionCapabilities Capabilities { get; }
    IFeedbackSubscription ObserveFeedback(FeedbackRoute[] routes);
    Task<OrderedBatchResult> SendOrderedBatchAsync(
        ControlLease owner, OrderedRpdoSyncBatch batch, Deadline deadline,
        CancellationToken ct);
    Task<ConnectionCloseResult> CloseAsync(Deadline deadline, CancellationToken ct);
}

public interface ITargetProgram
{
    ValueTask<RpdoPayload[]> ComputeAsync(CycleContext cycle, CancellationToken ct);
}
```

约束补充：

- 配置、输入、数据载荷、状态快照都做防御性复制/只读封装，不能用可被调用方修改的 byte[] 绕过代次和 DLC 校验。
- 每次异步生命周期调用都使用统一绝对 Deadline，不能给每条 RPDO 各自重置完整期限。
- Stop/Close 的 ct 只决定调用方等待期限与停止请求；取消不会撤销已经发出的停止，也不允许解除隔离。
- `Completion` 是服务接收订阅与发送/计算工作的实际完成，不等于物理 CAN 连接已关闭或电机已停止。
- 注册主键按 `ConnectionEpoch + ServiceName`，外部按连接名+服务名查找；同名连接重开后旧服务不能误指向新连接。

`CycleStatus` 至少输出：TxState、TxEpoch、InputVersion、CycleIndex、LastCompletedCycle、FailurePhase、目标/已提交/已确认帧数、Last/MaxStartLateness、Last/MaxCycleDuration、OverrunCount、LastError、IsolationReason。

`TargetStopResult` 至少输出：GenerationInvalidated、ComputeExited、TxWorkerExited、NativeCallsExited、OwnedTxQuiescent(bool?)、DriverQueueEmpty(bool?)、OnBusCompletionKnown、ConfirmationLevel、LastSubmittedCycle、LastConfirmedCycle、Confirmed、Error。OwnedTxQuiescent 指本服务该发送代次，DriverQueueEmpty 指驱动报告的队列范围，二者不能混淆。

## 7. 生命周期和异常状态

采集状态与发送状态分离，不用一个 Running 布尔值覆盖全部含义。

| 操作 | 采集结果 | 发送/连接结果 |
| --- | --- | --- |
| StartAcquisition | Starting -> Running | Targets=Idle，不提交目标。 |
| StartTargets | 保持 Running | Idle/Stopped -> Starting -> Running；先验证能力与控制占用。 |
| StopTargets | 保持 Running | Running -> Stopping -> StoppedConfirmed；失败则 StopTimedOut/Isolated。 |
| RestartTargets | 保持 Running | 同名启动默认报错；显式 Restart 必须先完成旧代次停止确认，再启动新代次。 |
| ServiceStop | 取消本采集订阅并等退出 | 先确认目标停止，再完成服务关闭；不关闭物理连接、不停止其他订阅。 |
| CAN_Close | 等待所有关联服务退出 | 关闭入口封锁新操作；收集实际工作及发送队列完成证据后才调用厂商 Close。 |
| 超时/不可中止 I/O | 能独立运行时保留采集 | 保留隔离和占用，不销毁底层句柄、不允许重启覆盖；不能返回已停止。 |

### 7.1 确认停止的顺序

1. 在生命周期锁内使发送代次失效，拒绝新的周期与输入应用。
2. 发出取消请求；计算返回、每次原生提交前后都复核代次。
3. 等待计算工作、发送工作及已进入的原生调用实际退出。`CancelAsync()` 不等价于这些工作完成。
4. 按厂商能力确认本服务旧帧不再位于驱动待发队列，或使用经过验证的队列取消契约。未经证明不清空整个共享发送队列，以免破坏别的协议。
5. 全部满足才返回 Confirmed=true 并释放目标控制占用。未能确认则保留隔离/身份及明确的失败阶段。

不能确认硬件队列时可报告 HostQuiescent=true、DriverQueueEmpty=null，但本次要求下不把它映射为“已确认停止”的 Completed。不存在“超时后清空资源表就算停止”的回退。

确认范围是本服务旧代次的目标/同步提交，而不是要求共享通道上的其他合法业务永远没有数据。该代次从未提交任何帧，或其全部提交早已取得足够确认时，可直接证明本代次没有待发帧，不必为了一个无关的全局队列 Unknown 阻塞只读服务退出。

### 7.2 StopTargets 与背景采集

StopTargets 不取消采集 token、不撤销 TPDO 订阅；仍可读取快照、诊断与后台故障。

但对同步触发 TPDO：仅保留接收任务不保证有新反馈。默认停止本执行单元的目标/SYNC 发送后，源可能不再产生同步 TPDO，此时如实变为 Stale/等待触发。不得把旧快照继续标成有效。

如原序列需要停止目标后继续 SYNC，必须显式设计同一单元的 SyncOnly 模式或交接给唯一的序列 SYNC 主者；不能启第二个不受控定时器。发生过 RPDO 成功而 SYNC 未确认的半周期时，先按设备协议处理待应用目标，不能自动进入 SyncOnly。此策略需原序列确认，首版不默认开启。

### 7.3 自动回收与物理连接

- 由连接资源统一协调任务，不能依赖资源表恰好先释放 service、再释放 adapter。
- 同步 Dispose 只启动幂等异步关闭与隔离，不在 UI/宿主回收线程无界等待；显式 ServiceStop/CAN_Close 等待其 Completion 并给出明确结果。
- 若 SDK 仅保证调用 IDisposable，不能声称宿主自动回收已经确认所有工作退出。由连接所有者延后真实句柄释放，记录未完成关闭；显式关闭仍是序列验收路径。
- 同名 Open 不再用 Set 直接销毁带活动服务的连接；默认拒绝，或显式替换并先确认旧连接关闭。同一物理通道的未退出占用不能通过换逻辑名称绕过。
- 旧通用周期发送器也登记实际 Task、CTS、所属连接代次及错误，参与同一关闭屏障；其 Stop/同名替换须等待旧 Task 退出。仍保留每报文独立定时器，不将其当作有序 RPDO/SYNC 实现。
- 通用周期任务键增加连接/代次，Stop 的 ConnectionName 开始真正参与定位。旧序列曾依赖“忽略连接名也能停止”的行为时，必须校正配置并在兼容说明中列明；不退回全局任务名匹配而误停其他连接。
- 真正卡死的原生调用没有安全的托管强杀方案。保留隔离及资源，等待实际退出或按厂商/外部安全流程处理；不伪造成功，不自动重连。

## 8. 序列步骤与 UI 草案

建议使用独立的六个步骤，最终 StepTypeId 在实施时检查全仓库唯一性后固定。

| 步骤候选名 | 主要配置/输出 | 语义 |
| --- | --- | --- |
| CANopen_ServiceStart | ConnectionName、ServiceName、FeedbackRoutes、每路 MaxAgeMs | 仅开始采集；列表配置原四路名称/COB-ID/DLC；时间能力不足明确告知。 |
| CANopen_CycleStart | 连接/服务名、NodeIds、RPDO 列表、SYNC、Period/预算、TargetProgram、RestartExisting | 开始有序目标发送；默认禁止重复启动和能力降级。 |
| CANopen_CycleInputsUpdate | 连接/服务名、InputVersion、输入值及有效期 | 序列中求值后一次发布；不改 TPDO/RPDO 定义。 |
| CANopen_SnapshotRead | 连接/服务名、显式结果变量路径 | 即时输出快照和周期状态；不等待四帧；缺失/过期/后台错误留在结构化输出。 |
| CANopen_CycleStop | 连接/服务名、StopTimeoutMs | 只停目标；须取得停止证据；不取消背景采集。 |
| CANopen_ServiceStop | 连接/服务名、StopTimeoutMs | 停目标并结束本采集订阅，确认后移除本服务。 |

SnapshotRead 查询本身成功时可以 Completed，但输出中必须保留每路 Valid、AllValid 和后台故障；提供显式 `RequireAllValid` 开关，开启时无效返回 Error，仍输出诊断快照。不得把 Completed 解释成四路反馈都有效。

所有输出使用用户配置且已声明的变量路径；推荐一个 Object/Struct 结果避免四路半更新。DTO/字典字段结构固定、提供序列访问示例；SDK 变量类型兼容性需契约测试，不凭文档假定可赋任意类型。

UI 使用各步骤独立 Editor/View/ViewModel：采集表格、目标/SYNC 表格、时间质量与能力提示、停止确认语义、输出变量配置；明确显示“不停轴、不使能、不重映射”和软件周期限制。集合修改必须能保存、刷新不覆盖已存数据。

例：`CAN_Open -> 原有设备初始化 -> ServiceStart -> SnapshotRead -> CycleStart -> 循环读取状态/按需更新输入 -> CycleStop -> 继续监控 -> 原有设备停轴/释能 -> ServiceStop -> CAN_Close`。具体运动与清理顺序仍由原序列和设备规范决定。

## 9. 拟修改文件与兼容策略

| 位置 | 计划变更 |
| --- | --- |
| `CAN/CanPlugin/CANopen/CanopenConnectionAccess.cs` | 接入共享路由、观察订阅、队列指标；不另开协议原始读者；分离 SDO 事务门与 TPDO 观察。 |
| `CAN/CanPlugin/CANopen/Cyclic/`（新） | 服务、不可变数据契约、时间质量、单周期调度、输入发布、控制占用和确认退出。 |
| `CAN/CanPlugin/Adapters/ICanAdapter.cs` 与可选能力接口 | 保留既有接口兼容；新增接收时间/队列统计/发送确认的可选能力，不支持时明确 Unknown。 |
| `CAN/CanPlugin/Models/CanMessage.cs`、厂商适配器 | 原时间戳保留；新增可选元数据；元数据在最早可证明的入口创建，不能在出队时伪造采样时间。 |
| `CAN/CanPlugin/Executors/CanOpenExecutor.cs` / `CanCloseExecutor.cs` | 注册统一连接所有者，关闭屏障、重复打开策略、超时隔离。 |
| 旧 CAN_Read/Write、UDS/XCP 入口 | 仅接入受控模式的前置兼容/占用检查；不重写协议本身；禁止未路由读者和冲突写者。 |
| 旧 CanCyclicSendStart/Stop | 补实际 Task 跟踪、连接级任务键、停止/替换确认与关闭屏障；保持原独立报文定时器语义，不承担 RPDO/SYNC 顺序。 |
| `CAN/CanPlugin` 新步骤及 `CAN/CanPlugin.UI` 对应 UI | 独立设置/序列化/校验/执行/界面，旧 StepTypeId 与旧设置不静默迁移。 |
| `CAN/CanPlugin.Tests/`（拟新） | 假适配器、可控时钟、可控阻塞 I/O、路由/快照/顺序/退出/编辑器回归。 |

旧通用周期步骤仍用于普通报文，不升级其含义为安全周期控制。新模式下冲突会显式拒绝；旧模式尽量保持原行为。最终版本号在实施前读取远端与本地后按项目原有方式递增，本轮不改版本。

CAN 核心接口为 CiA402 依赖，后续须全量回归现有 CiA402 用例，但本次设计不要求修改其步骤或已完成的恢复逻辑。不能把旧包与新包的私有 CAN 依赖随意混放；实施时核查加载与回退。

## 10. 分阶段实施与验收计划

以下全部是未来实施任务，当前未实现、未运行新功能测试。

1. **能力验证与契约冻结**：先核实 NI/ZLG 等 SDK 的时钟域、发送完成/FIFO/排空能力、SDK 资源异步回收边界与独立求值上下文；形成明确 Supported/Unsupported。不得以统一接口代替硬件能力证明。
2. **共享连接与接收路由**：最小实现唯一读取者、SDO 应答保留、TPDO 观察订阅、有限队列/分层计数和连接代次；回归旧 CANopen 与 CiA402。
3. **时间快照**：不可变最新值、无帧即时返回、DLC 错误、来源时间域、可信年龄或 Unknown；验证重复读不改变帧龄。
4. **单周期与故障状态**：同一个工作单元计算并提交 RPDO/SYNC；显式输入版本、过期/超时拒绝、发送顺序能力检查、超限/半周期状态，不追加旧目标。
5. **生命周期接入**：停止代次、实际退出等待、驱动发送确认、重复启动、CAN_Close/自动 Dispose；注入不可取消原生调用，验证隔离不可绕过。
6. **步骤/UI/交付**：新增独立步骤及编辑器，完成配置保存与变量/生命周期校验；构建 CAN 核心/UI、跑新测试和 CiA402 回归，检查 UTF-8，备份后生成完整目录；按项目中文提交规范另行执行授权提交。

### 必须定义的回归场景

- 四路 TPDO 任意顺序到达、某路缺失时快照读取不等待；读取不消耗 SDO 应答、不修改序号。
- 旧帧在驱动/适配器/共享 FIFO 积压后被取出，不以出队时间变成新数据；不支持年龄证明时必须 Unknown。
- 时间回拨/回绕/重连、重复来源时间、DLC 错误、未知队列计数，不出现伪 Valid 或伪 0 丢帧。
- 接收压力与 SDO 并行；不同等待者无整连接读门阻塞，已有 SDO 请求/应答保持一致性；原始读者在发送前被拒绝。
- 一周期多个 RPDO 严格按配置提交，再提交 SYNC；RPDO 失败不发 SYNC，SYNC 失败暴露半周期；无追赶补发。
- 计算同步阻塞/异步忽略取消、原生写阻塞、日志阻塞、关闭与重启并发，均无旧代次晚到发送、无提前句柄释放、无假停止确认。
- 旧通用周期任务也不能越过 CAN_Close 屏障；切换受控模式时旧读取仍在运行，应拒绝/等待而不是双读。旧 Stop 配置错误的连接名必须报清楚，不能误停别的连接。
- StopTargets 后仍采集；同步 TPDO 因无 SYNC 而停止更新时如实 Stale；不得自动从半周期故障恢复 SYNC。
- 同名服务在不同连接不冲突；同一物理连接/节点不能借改名绕过隔离或重复 SYNC 主者。
- 无硬件确认能力的适配器拒绝强保证；有序方法调用测试不代替真实总线 RPDO-before-SYNC 抓包。
- WPF 配置表格增删/提交/刷新/保存、显式变量类型和页面布局；核心/UI Release 构建；现有 CiA402 回归；实际板卡时序/队列/关闭仍单列硬件验收。

## 11. 本轮交付与设计评审

- [x] 确认本轮只做方案，四路协议由序列配置。
- [x] 复核目标源码及云端开发手册；保存 L3 分阶段方案。
- [x] 按 ECC planner、architect、.NET patterns 与当前 Agent 审查方法核对架构、兼容、时间、取消和资源边界；没有启动子 Agent。
- [ ] 未实现接口或 UI，未运行新功能单元/集成测试，未构建新插件，未做硬件验证。
- [ ] 实施前须完成厂商能力验证；停止后的持续 SYNC 策略需结合原序列确认。其余配置不要求把设备组包规则写进插件。

设计评审结论：可作为下一轮实施输入；不应在时间来源与发送确认能力未验证时直接承诺现场功能。重点风险已转成显式能力、失败状态或前置验收条件，没有用默认成功隐藏未知。

### 交付自检

总体 4.0/5，无 2 分及以下项；结论为交付设计草案，不进入实施。

| 维度 | 分数 | 证据与下一步改进 |
| --- | --- | --- |
| 准确性 | 4 | 源码、云端手册已核对；厂商时钟/确认能力仍是实施前置验证，不声称已支持。 |
| 完整性 | 4 | 三项范围、接口、步骤、生命周期及验收均覆盖；持续 SYNC 策略仍须原序列确认。 |
| 清晰度 | 4 | 区分观测时间/采集时间、软件退出/物理停轴；后续可按实际序列补一个配置样例。 |
| 可操作性 | 4 | 六阶段和拟改文件明确；接口尚未编译验证，符合本轮只做方案的边界。 |
| 精简性 | 4 | 审计细节在文档，交付摘要仅保留关键决策；后续可随契约冻结删去已关闭的选项。 |

优先改进：实施前完成适配器能力验证，再用原序列核对停止后 SYNC 策略。自检判断：用户能够审阅并决定后续实现，不会把这份方案误当作已编译插件或实机验收结果。
