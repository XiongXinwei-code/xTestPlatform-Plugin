# 运动代次停止确认与 ZLG 受控 CANopen 适配

任务等级：L3。范围限于 CAN 插件、对应测试/说明和配套发布，不改实际序列、不操作设备、不提交现有混合工作区。

## 阶段与验收

1. 先以可控的异步恢复屏障及延迟确认适配器复现：运动停止完成后，新监测 SYNC 在途，不应使本次停止失败。故障、PartialCycle、旧代次仍须拒绝。
2. 为每次运动保存不可变停止结果，以捕获的运动标识和结果确认停止；全服务发送计数仅用于整体发送诊断/关闭。
3. 核对随库 ZLG 官方头文件/设备 XML，增加受支持设备的回显确认、共享接收和保守时间信息；未知设备能力明确拒绝严格发送，普通写入不伪造确认。
4. 反馈订阅继续支持 1~128 路配置，新增非四路、独立序号/有效期验证。UI 不引入四路假设；序列自己做单位转换和停稳判据。
5. 执行 CAN 全量、CiA402 调用方回归、核心/UI 发布构建、UTF-8/差异检查及独立只读复审；生成可直接替换的插件目录。

## 架构和兼容

- 单连接仍只有一个 SYNC 调度者。停止结果属于某次运动，Monitoring 的后续计数变化不反向改变它；新运动启动后旧确认必须拒绝。
- 保留错误锁存、PartialCycle、未知待发和真实 I/O 未退出隔离；ACK 不代表物理停稳，正常监测 HostExited=false。
- ZLG 复用现有 ICanConfirmedTransmitter/ICanClockValidity/ICanCloseState/ICanTransmitQuiescence 共享契约；接收泵只读一次驱动，发送回显不进入 CANopen 普通路由。
- ZLG 时间若只能建立发送回显因果下界，则明确该下界来自发送前主机时刻；无锚点/过期/时钟异常保持 TimeUnknown。不能以接收出队时刻推定采集时刻。
- ZLG 严格能力按有证据的型号和属性检测开放；保留普通收发兼容，不以 Transmit 返回数量当作总线确认。
- 回退：关闭平台恢复完整旧版 CAN 文件夹，停用新增适配能力；不覆盖旧发布目录。

## ECC 能力与风险

当前 Codex 采用 planner、architect、csharp-reviewer、silent-failure-hunter 方法及 tdd-workflow、verification-loop；工作区已有有效测试，使用 dotnet/xUnit。AGENTS 要求独立审查，复用 can_review 子 Agent，只读审查共享状态、旧代次、异常和协议边界。框架技能中的 checkpoint commit 不用于当前已有多模块未提交修改，证据保存在本文件，避免未经要求提交用户工作。覆盖率未测量不声称达标。

关键风险：停止等待者恢复与下一监测发送交错；旧停止影响新运动；重复/迟到回显关联；底层阻塞未退出；设备时钟回退；Classic 与 FD 队列并存。软件假驱动不能代替真实 NI/ZLG 或电机验证。

## 验证记录

### 失败证据与修复

- 运动停止 RED：`MotionStopTests.CompletedMotionStopIgnoresNextMonitoringSyncAwaitingConfirmation`（现已扩展为三场景 Theory）通过 AsyncLocal 的异步 ExecutionContext 恢复屏障暂停停止等待者，同时让下一监测 SYNC 进入延迟确认。原实现编译并执行后在 CanopenCyclicService:429 报“停止未确认”，130ms 内失败。证据：`artifacts/can-validation/results/motion-stop-red.trx`。修复后同窗口通过，没有修改生产超时、错误计数或 RequireOnBusConfirmation。
- ZLG 能力 RED：新假驱动测试在缺少 ZlgIo 适配边界时编译失败；实现后覆盖提交并非完成、native 未退出、同载荷重复旧回显、取消保留、晚回显关闭、接收泵未退出、Close 失败重试、重新打开时间归零、共享路由 owner SDO 停机。
- 时钟 RED：异常计数回退后旧实现立即加新锚点，`CounterJumpInvalidatesClockAndNeedsLaterEchoToEstablishNewAnchor` 实测失败。改为异常那次保持未知、等之后回显再建锚点。证据：`stop-clock-regression.trx`。
- 历史超时 RED：已 faulted 接收任务的 TimeoutException 曾被误判“任务未退出”。`HistoricalReceiveTimeoutIsReportedAfterActualHandleRelease` 实测失败；改为只有任务尚未完成才分类为等待超时。原故障在安全释放后仍报告，未吞掉。证据：`zlg-receive-error-red.trx`。
- 独立审查发现 Classic/FD 分队列只匹配队首可能丢回显、reopen 未清旧边界、Clock.Reset 锁外竞争。均修复并补测；混合队列测试初版 fake 曾错误复用上次 FD 回显，已纠正为各 native API 分别入队，终版实际覆盖不同出队顺序。未将这个初版 fake 失败当作缺陷成功复现证据。
- 全量初跑 4 项失败（3 项既有短期限测试及 1 项 ZLG 集成）：故意阻塞 native/ExecutionContext 的新增测试与其他类并行，挤占进程全局线程池。仅为这些阻塞类定义 DisableParallelization 测试 collection，类内显式并发仍实际运行。未增加生产期限、清除错误或跳过测试；随后全量 76/76。

### 最终验证（2026-10-23）

| 检查 | 实际命令/操作 | 结果 |
| --- | --- | --- |
| CAN 全量及19项新增 | `dotnet test CAN/CanPlugin.Tests/CanPlugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/can-validation/stop-zlg-tests --logger trx --results-directory artifacts/can-validation/final-results -v minimal` | 76/76通过，0跳过；TRX：`lei.zeng_RCS26100N_2026-10-23_19_25_56_net8.0.trx` |
| CiA402 调用方 | `dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/can-validation/cia-stop-zlg-regression --logger trx --results-directory artifacts/can-validation/final-results -v minimal` | 204/204通过，0跳过；TRX：`lei.zeng_RCS26100N_2026-10-23_19_27_06_net8.0.trx` |
| 发布核心/UI | `dotnet build CAN/CanPlugin.UI/CanPlugin.UI.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/Release/CAN-1.0.33/Plugins/CAN -v minimal` | 成功，0警告0错误。全量测试编译另有既有 `CanopenExecutorHelper.cs:100` CS8601，未声称全仓库无警告 |
| 格式 | `dotnet format whitespace CAN/CanPlugin/CanPlugin.csproj --no-restore --verify-no-changes --include CAN/CanPlugin/Adapters/Zlg/ZlgAdapter.Runtime.cs CAN/CanPlugin/Adapters/Zlg/ZlgEchoClock.cs CAN/CanPlugin/Adapters/Zlg/ZlgIo.cs --verbosity quiet` | 首检3处换行格式已修，终检退出0 |
| 差异 | `git diff --check` | 通过 |
| UTF-8/发布内容 | `node artifacts/can-validation/verify-stop-zlg.mjs` | CAN 255份源码/项目/说明严格UTF-8解码通过；发布159个文件，47.66MiB，无testhost/xUnit/ContractTests程序集；两DLL FileVersion均1.0.33.0 |
| 独立复审 | 原生 can_review 子Agent，只读读取相关源码和测试设计 | 最终通过，先前问题已关闭，未发现新的阻塞级风险；不将其当作测试/实卡证据 |
| 覆盖率 | 未执行覆盖率测量 | 不声称80%或其他覆盖率门槛达标 |
| 实卡/完整宿主 | 无目标 NI/ZLG 板卡和已安装NI驱动，未启动真实平台 | 未执行，不能宣称现场通过；UI本轮无布局修改，已有STA组件测试随CAN全量运行 |

### 行为验收

| 保证 | 证据 |
| --- | --- |
| 新监测帧在途不影响已完成运动停止；旧等待遇新运动不会隔离它；后续监测错误仍锁存 | MotionStopTests 三场景，均通过 |
| 故障/PartialCycle/native未退出不伪成功；关闭仍等全部在途发送 | 既有 SchedulerTests/RuntimeTests/NiDriverTests及新增ZlgRuntimeTests，全量通过 |
| 反馈数量1/3/7/128、独立序号与陈旧判定，129拒绝 | ConfigurableFeedbackTests，5项通过 |
| ZLG echo/native成功退出同时满足才确认，普通接收不含echo，跨队列/重开/晚确认/释放错误 | ZlgRuntimeTests，9项通过 |
| 不以出队时刻刷新，旧帧/无锚点/过期/异常那次映射无效 | ZlgClockTests，2项通过 |

### 审查和交付边界

C#主审查核对共享类型兼容、调用方、锁顺序、native任务生命周期；异常专项复核停止结果发布、历史接收超时、未确认票据与错误锁存。ZLG capability仅对随库 XML 有证据且属性被接受的100U/200U开放，未经核对的其他型号保持严格启动拒绝。原始帧DLC/长度、接收返回数量均验证，驱动错误/队列未知统计不伪造为0。

PowerShell 在验证后期发生启动无输出，已终止本任务的挂起会话，改用 cmd 调用相同 dotnet/rg/git 命令；UTF8/发布版本由只读Node脚本复核。这不是CAN运行故障。未安装驱动、未修改模型/审批设置、未发送设备命令。

发布目录：`artifacts/Release/CAN-1.0.33/Plugins/CAN`，带完整依赖、部署说明和 CANopen-Guide.md，旧1.0.32保留。工作区已有CiA402修改保持不动，本轮未commit/push。实际部署机仍需验证专有驱动ABI、回显物理含义、时钟/拔线/BusOff、真实周期及物理停稳；本版软件定时不保证硬实时。

DLL SHA256：

- CAN.StepPlugin.dll：`dc0f2f17cfd568a4f9805d8345ce55c351667f5a04957eb95ad8b7f73b363718`
- CAN.StepPlugin.UI.dll：`d8f0f10665db813962a5da3af45010aa18e0445bc2cfcf56207749a2a415d781`

状态：本轮软件修复、受控ZLG适配、构建和模拟验证完成；硬件及完整宿主验证未完成，按上述边界交付。
