# CAN 版本取证回执：V-01～V-10

任务等级：L2，只读诊断与取证；采用 ECC planner、C# reviewer 和异常传播审查方法。未修改、重编译或替换插件，未操作真实设备；新增的仅为本回执和 artifacts 内的取证脚本、源码快照、纯托管探针及结果。

## 1. 取证对象与边界

本轮读取两份**本地现有产物**，并验证 PE / Portable PDB 的 CodeView GUID、时间标识及 PDB 文档 SHA-256；不是仅以工作区源码推定 DLL 内容，也不是宣称已做完整反编译。V-06 另直接读取 IL。当前交付包中的6个相关源码文件均与其匹配 PDB 的编译输入哈希一致，逐字节快照保存在 `artifacts/plugin-version-audit/source/`。

尚未取得现场实际加载 DLL 的路径/文件，因此不能将下面任一身份擅自标为“本次现场运行 DLL”。三份2026-10-23原取证文档未在本仓库检索到，也未随本消息提供；本回执核对的是问题单所引断言，不宣称重新阅读过三份原文。

### 1.1 本地2026-10-24最终交付包

位置：`artifacts/Release/2026-10-24-audit-fixes/Plugins/CAN/CAN.StepPlugin.dll`。

| 字段 | 实测值 |
| --- | --- |
| AssemblyVersion | `1.0.34.0` |
| FileVersion | `1.0.34.0` |
| ProductVersion | `1.0.34+5351a0a9d66594455b292fe65308303488a07537` |
| MVID | `c9e74f1b-7f6a-4f16-bb39-fcd9fc04f6a8` |
| SHA-256 | `5e458ee099f22599e19425a4db3938b4fc7733b4de410561932625f824570f11` |
| 字节数 | `546816` |
| PDB SHA-256 | `22d8defc1bd36a4a19193aa3a93609ec417f3735a7bb0074e6ad8a62ab1081c7` |

配套 `Plugins/CiA402/CAN.StepPlugin.dll` 与上述 DLL 的 MVID、SHA-256 完全相同。当前源码含未提交修改，ProductVersion 中的 Git HEAD 不能单独定位本地实际编译输入。

原始记录：[交付包元数据](../../artifacts/plugin-version-audit/delivery-20261024.json)、[CiA402私有副本](../../artifacts/plugin-version-audit/cia-private-20261024.json)。

### 1.2 本地2026-10-23中间包

位置：`artifacts/Release/2026-10-23-audit-fixes/Plugins/CAN/CAN.StepPlugin.dll`。它不是那三份原文所用 DLL 的已确认身份。

| 字段 | 实测值 |
| --- | --- |
| AssemblyVersion / FileVersion | `1.0.34.0` / `1.0.34.0` |
| MVID | `194f4d9c-3237-469c-8e86-aed850ff2d40` |
| SHA-256 | `792feb654999e90b7e45cab5fb30334019dc7be639b95ba1e0b9e894255c1afe` |
| 字节数 | `527360` |

两份产物版本号相同，但不是同一二进制。必须使用 MVID / SHA / 位置区分，不能仅凭“1.0.34”作回执。原始记录：[中间包元数据与PDB锚点](../../artifacts/plugin-version-audit/intermediate-20261023.json)。

## 2. V-01～V-10逐条回执

以下行号均对应本轮已用 PDB 文档哈希核对的当前交付源码。以“仍成立 / 已变更或需限定 / 原断言不成立”表达事实；原推断不成立不等于某次代码修复已被证实，不能全部强行归为“已修复”。

| 编号 | 结论 | 当前代码证据及问题单处理 |
| --- | --- | --- |
| V-01 | 核心判据仍成立，位置需修正 | `CanopenCyclicService.cs:330–352`：比较在 `SendAsync`，`period` 是运动的 `PeriodMs` 或监测的 `MonitoringPeriodMs`；`_lastDuration > period` 即抛“发送周期执行超限”，不加容差。`RunAsync:286–300` 保存异常并进入故障。DEF-02可保留该判据，不能继续写比较位于RunAsync。 |
| V-02 | “仅校验、不参与判定”不成立 | `CanopenCyclicService.cs:249–250` 用运动/监测各自 `MaxLatenessMs` 判定**周期开始迟到**，并禁止追赶补发；`CyclicExecutor.cs:38/58`传入配置。它不参与V-01的**发送批次耗时**容差，故DEF-03不能标“死参数”，可讨论两类期限的语义/配置清晰度。 |
| V-03 | 仍有该隔离判定，但必须限定分支 | `ConfirmExit:468–471`：提交/确认不等且不满足 `closingConnection && _lateQuiescence` 时才置服务状态Isolated并抛错；关闭连接专用路径有迟到最终确认例外。PartialCycle在非关闭连接路径仍保留占用。不能删掉这些保护，也不能称所有关闭一律无法退出。 |
| V-04 | 部分成立、已与引文不同 | `StopTargetsAsync:426–440` 只包围停止等待。捕获的旧代次不能修改新代次；监测模式已完成的运动捕获结果可在435–437直接返回。其余等待失败才置Isolated、保留错误并调用连接Isolate。451行的历史故障复抛在该catch**之外**，不能将两条路径合并。 |
| V-05 | 对ServiceStop仍成立 | `CloseAsync:479–483` 先await StopTargetsAsync，之后才RequestStop、确认退出、Dispose订阅并置closed；此前抛异常就不执行后续。`CanConnectionResource.StopServiceAsync:198–200` 只有成功后才移除服务。整连接CAN_Close走另一路CloseForConnectionAsync，必须区分。 |
| V-06 | 仍成立，属于能力边界 | `CanRuntimeContracts.cs:63` 为恒false getter、无setter；实际IL为 `16 2A`（ldc.i4.0; ret）。它明确表示插件没有提供物理停稳证明，不是把通信ACK当作已停稳。若DEF-05要求新增物理停稳验证能力，这项能力确实尚未实现。 |
| V-07 | 组合推断不成立，应撤回“重新发送导致必然复抛” | `StopTargetsAsync`没有调用本类SendAsync；451行是 `if (capturedFault != null) throw ...`，属于监测分支对已记录错误的重新抛出。停止运行中的运动仍可能让worker经CompleteMotionAsync执行已配置的停机SDO（356–372），不能反过来声称“任何停止都不发送报文”。本轮纯监测故障复现中停止前后发送数均为1，仍复抛。 |
| V-08 | 一半成立，“异常仅Warn”已不符 | `CanCyclicSendStartExecutor.cs:29–48` 逐报文PeriodicTimer，没有本批耗时超过周期的比较；但46行取消同组并重抛，`CanBackgroundJob:550–563`保存Error，StopAsync会抛出，整连接关闭也保留错误，不是只Warn。旧路径不报周期超限，只能用于说明策略不同，不能证明其满足严格时序/发送确认。 |
| V-09 | “一切后续操作”不成立，且混淆两层状态 | `CanConnectionResource.CheckLocked:159–164` 在连接 `_isolation != null` 时拒绝受保护I/O/启动；服务的 `State="Isolated"` 不自动等于连接已设置 `_isolation`。Snapshot、GetService和关闭入口没有统一被这行拒绝，关闭专用路径仍可进行确认/释放。 |
| V-10 | StartTargets仍1～128；ServiceStart不受该目标列表约束 | `CopyFrames:54–60` 和 `StartMotion:153`仍拒绝0个运动RPDO。`CyclicExecutor:26–40`的ServiceStart不读取Targets，显式EnableMonitoringSync才调用StartMonitoring；默认仍仅接收。有限轨迹可让设置Targets为空，但每个时间点Frames仍须非空。用户的SYNC-only运行可反证“不存在SYNC-only”，不能反证StartTargets允许0目标。 |

源码快照：[周期服务](../../artifacts/plugin-version-audit/source/CAN/CanPlugin/Runtime/CanopenCyclicService.cs)、[连接资源](../../artifacts/plugin-version-audit/source/CAN/CanPlugin/Runtime/CanConnectionResource.cs)、[配置](../../artifacts/plugin-version-audit/source/CAN/CanPlugin/CANopen/Cyclic/CyclicSettings.cs)、[周期执行器](../../artifacts/plugin-version-audit/source/CAN/CanPlugin/CANopen/Cyclic/CyclicExecutor.cs)、[状态契约](../../artifacts/plugin-version-audit/source/CAN/CanPlugin/Runtime/CanRuntimeContracts.cs)、[通用周期执行器](../../artifacts/plugin-version-audit/source/CAN/CanPlugin/Executors/CanCyclicSendStartExecutor.cs)。

### V-01计时范围的额外澄清

Stopwatch在启动发送Task.Run之前开始计时，并在await恢复后读取，因此包含线程池排队、发送门等待、驱动/回显确认以及continuation调度等待；不是纯粹的总线上线时长测量。单凭该异常不能归因于NI硬件发送慢。MaxLatenessMs调大也不会放宽352行的批次耗时判据。

## 3. 与2026-10-23本地中间包的三类差异

| 类 | 编译输入是否变化 | 对本清单的影响 |
| --- | --- | --- |
| CanopenCyclicService | 未变化，PDB文档SHA同为 `b1d934f7cb28458a476f8aeef796ce282202cc603028327470a0ab1ea0b5a1ff` | V-01/02/03/04/05/07/10的相关实现不是23中间包到24交付包之间新修改的，不能将与引文不符都解释成“24日修好了”。 |
| CyclicSetting（文件CyclicSettings.cs） | 未变化，SHA同为 `e641cc9de5be27e7311aaac492a5e34f8b8884b2c14f9707d3f066880f02fcae` | 监测SYNC配置和Targets默认空列表在两包均存在。 |
| CanConnectionResource | 已变化，旧PDB文档SHA `e6e7c636314e5395b1730c9524c9cd4c5ce2933238981f65da7ee337c38c4e84`，新为 `064f6614070c28cad42d3a59615f7b085d3087415e871d9d084a6578fbb1337d` | 当前增加原生Open生命周期、有界重开等待、原始读取消、Write期限/最终提交检查和精确实例释放等；V-09及整连接收尾相关，但未改变上表服务级CloseAsync的短路行为。 |

比较依据为两包各自匹配的PDB，不以文件日期推定版本。`source-receipt.json`另明确：手边更早的本地baseline连接资源源码与23中间包PDB的哈希**不相同**，不能把该baseline冒充23中间包的精确源码；它仅能作为早期路径对照，本文不使用它证明旧包全部改动细节。

## 4. 运行时行号锚点复核

直接从两个匹配PDB的方法序列点读取：

| 锚点 | 23本地中间包 | 24最终交付包 |
| --- | --- | --- |
| SendAsync / RunAsync | 352 / 263 | 352 / 263 |
| StopTargetsAsync / CloseAsync / CyclicExecutor.ExecuteAsync | 451 / 480 / 76 | 451 / 480 / 76 |
| CanConnectionResource.StopServiceAsync中的await | **141** | **199** |

用户日志所报StopServiceAsync:121与这两包均不一致；更早baseline源码确有121行，但其精确现场二进制身份未提供。应保留原日志作为该次运行的符号锚点，不能凭其中几个未变化的行号认定已加载24最终交付包。行号来自编译时PDB，不保证等于后来工作区同路径文件的对应内容。

## 5. 本轮实际模拟验证与未修事项

探针仅引用**交付DLL**，没有ProjectReference到CAN源码。进程自报MVID / SHA与1.1节完全相同；适配器为纯托管fake，没有连接NI/ZLG或电机。原始结果：[behavior-receipt.json](../../artifacts/plugin-version-audit/behavior-receipt.json)。

1. **纯监测超期与服务级收尾**：监测周期10ms，MaxLatenessMs=5000，fake确认发送延迟40ms。实际记录LastDurationMs=49.5685，发送/确认=1/1、HostExited=true、OwnedTxQuiescent=true。StopService复抛历史错误，发送数从1到1，没有重新发送；服务仍登记。随后整连接Close报告历史错误，但IsClosed=true，同物理键重新创建成功。
2. **“worker故障后必然复抛”的反例**：无监测运动因InputMaxAgeMs过期进入Faulted且HostExited=true，直接StopService成功移除服务。这是服务资源API的结果；步骤执行器仍会按历史Error返回失败，未将故障伪装成成功。

因此DEF-01宜拆成“故障监测服务StopService复抛历史错误，导致本可确认退出的服务未完成释放”与“真实待发/PartialCycle必须保留隔离”两类，不能统称永久锁死。前一项在本次交付DLL上已模拟复现，**尚未修复**；整连接可关闭不等于服务级问题已修复。DEF-02当前判据仍存在，DEF-03死参数论证不成立，V-07组合机制应撤回。

后续若修复，需要将“保留历史Error”与“已确认安全的资源释放”分离，而不是忽略Error、强改计数、关闭确认或丢弃未退出任务。本轮只交付回执，没有进行该修复。此前545项回归未覆盖上述纯监测故障收尾路径，不能用既有通过结果将新发现标成已修复。

## 6. 执行记录与剩余边界

- PE/PDB身份、6个源码文档哈希与叶属性IL读取通过；源码快照逐字节哈希校验通过，两个包的方法行号锚点已读取。
- 探针离线restore首次受NuGet用户配置读取权限限制未执行成功；经授权只读配置后restore/build成功，0警告/0错误；两个受控场景实际运行通过，并记录实际加载DLL身份。没有构建CAN工程，也未改变交付DLL。
- 本轮生成文件均为UTF-8；差异自查已执行。未运行全项目构建/545回归，因为本轮未改插件；不将历史测试冒充本次执行。
- 尚缺现场实际加载DLL/路径，三份原取证文档原文也未取得。没有现场总线、硬件停稳、驱动队列或完整平台运行结论。
- 取证脚本、JSON、源码快照和探针位置：`artifacts/plugin-version-audit/`。未提交/推送；不覆盖运行平台目录或原交付产物。
