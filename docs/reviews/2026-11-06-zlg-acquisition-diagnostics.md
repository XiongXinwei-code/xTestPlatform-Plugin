# ZLG EtherCAT 采集诊断与步骤划分验收（L3，2026-11-06）

最终产物为本地 Release ZIP：`artifacts/Release/ZlgEtherCat-v1.0.0.zip`。公开 **97 个步骤、109 个操作**：90 个固定步骤没有功能下拉，7 个相似操作组有功能下拉。未部署、未提交、未对设备发出命令。

## 使用和分界

- `ZlgEtherCat.Open`、`BeginExit`、`Close` 独立；总线启停、过程映像使能/禁用、采集启停、周期输出启停、SDO 读/写、板载动作启停、轴绑定/解绑及轴使能/去使能分别独立。轴运行、回零和停止不合并。
- 七个组合步骤为 `PdoRead`（PDO 字节/标量或位域读取）、`PdoWrite`（普通/掩码写入）、`MasterDiagnostics`、`SlaveDiagnostics`、`BoardDigitalRead`、`AxisRead`、`CyclicTarget`。同步目标保留各自模式校验及 CSP/CSV/CST 的 `int` / `int` / `short` 语义。完整清单见 `ZlgEtherCat/README.md`。
- 每通道一个原有 SDK 工作线程负责 `EcatPIInputQueuePop`。后台与周期读取继续各用一个原有功能池；增减读取项仅改变配置。两个池各自提供创建、添加、更新、停项、查询和关闭步骤。
- `StartAcquisition` 独立页面/平台助手可配置 `AcquisitionThreadPriority`（Normal/AboveNormal/Highest，默认 Normal）、`RxHighWatermarkPercent`（80，1～100）、`RxBacklogWarningMs`（10，1～60000）、`RxAlarmRepeatMs`（1000，100～3600000）。启动时在共享 SDK 线程设置并回读优先级；停止后恢复。失败保留错误，不能把失败启动当作成功。
- `ReadAcquisitionStatus` 从缓存读取 `Priority`、`RxQueue`，SDK 调用未退出时仍可查看上次观测、年龄及轮询间隔。满深度只锁存疑似覆盖，不提供准确硬件丢帧数。停止保留证据，成功新启动重置；恢复通知和其它队列通知共享间隔。FIFO、LatestOnly 主动清旧、主机历史淘汰与软件跳样分别表达。
- 时间戳来源为主机完成读取后的单调时钟，提供频率，`HardwareTimestampAvailable=false`、`HardwareTimestamp=null`；本地年龄不含读取前的 RX 排队时间。未把它标为板卡接收时间或硬件采样年龄。

## 执行和证据

证据目录为 `artifacts/zlg-validation/2026-11-06-acquisition/`。下列检查对应最终文件；28 步和早期 `integration-first`、`tests-second-full`、`layout-tests-final` 等记录仅用于追溯中间版本。

| 检查 | 实际操作与结果 |
| --- | --- |
| 开发手册 | 实施前已核对远端 master 手册 SHA `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`。§18.1/18.3 的单动作要求仅按用户明确授权对七个同类组合例外；未声称 109 个动作各自对应独立步骤。其它设置、编辑器、表达式、校验和编译要求照常执行。 |
| 构建 | UI 项目连同 Core，`dotnet build ... -c Release -t:Rebuild -m:1 -r win-x64 -p:Platform=x64 -p:SelfContained=false -p:AppendRuntimeIdentifierToOutputPath=false --no-restore`，0 警告、0 错误；`layout-final-build-console.log`。 |
| 主套件 | `dotnet test ZlgEtherCat/ZlgEtherCatPlugin.Tests/ZlgEtherCatPlugin.Tests.csproj -c Release -p:Platform=x64 --no-restore`，**208/208**，0 跳过；`package-tests.trx`。包含 109 个操作分别经过执行器边界、实际 WPF 控件选择/保存/刷新/重入、步骤隔离、参数校验、采集与故障路径。执行器设备端为测试替身。 |
| 运动/工作线程独立项目 | `dotnet test ZlgEtherCat/ZlgEtherCatPlugin.Tests/Motion.UnitTests.csproj -c Release --no-restore`，**31/31**；`motion-final.trx`。该套件与主套件部分重叠，不相加当作独立覆盖率。 |
| RX 核心规则 | 独立纯状态机 10 项先在未实现骨架失败，完成后 10/10 通过；`../rxmonitor-tests/results/rxmonitor-red.trx`、`rxmonitor-green.trx`。相应最终场景也在主套件中运行。 |
| 平台助手 | 安装宿主 `DescribeFields` / `FormatValue` / `ConvertText` 与实际设置序列化，对最终 DLL 检查 **97/97 步骤、109/109 操作、1462 次参数往返、12 处非活动字段变更**，包括全部合法枚举值；严格负例 **904/904 拒绝**，源设置未改变；`layout-assistant-109-operations.json`、`layout-assistant-generic.json`。这是脱离现场工程的宿主纯方法验证。 |
| 生成与资源 | 790 个生成文件与重新生成内容逐字节一致（只读比较）；`layout-generation.log`。`StepInfoExtractor --strict` 识别 97 个入口，`layout-metadata.json`。每个入口有独立编辑器，实际 XAML 中只有上述七个功能下拉。 |
| 源码检查 | `tools/Verify-PluginSources.ps1` 通过，仓库范围 2722 个文本文件、264 个独立编辑器描述；`final-source-check.log`。本次变更涉及的既有跟踪文件通过定向 `git diff --check`。 |
| 打包 | 按项目单插件包布局，编译目录 23 文件，ZIP 加顶层 `icon.png` 后 24 个。全部 ZIP 条目逐一核验 SHA256；测试 DLL 与包内 Core/UI 同哈希。仅两份标准 `.deps.json`，无 Markdown 或开发目录 JSON。`package-manifest.json`。 |

最终 SHA256：ZIP `7F0191D15179B6A8FB46932C3A094A59FF32A48F829174B270751A0D49FFFD6D`；Core `744F88CF4E8453915CD18AC833D97410ECDD48FFF4034B0D4D755320F608346A`；UI `2A58FF22723B93B676DA1B70902039349733FBE71D8B81A4706692481B3F176A`。

## 审查和修复闭环

按 ECC planner/architect 方法划分公开契约、通道并发及回退范围；采用 TDD、C# reviewer、异常传播审查、输入边界检查、WPF 运行验证及 verification-loop。独立审查实际读取源码、运行/序列化调用链和报告，未执行设备动作。

1. 独立审查发现慢日志可能阻止原生关闭开始。已在现有 SDK 线程发起设备清理，同时等待已有池内回调退出，两者结束后才释放通道归属；优先级恢复失败也执行设备清理。阻塞日志中观察到原生关闭已完成、归属尚未释放，退出回调后释放；已复审。
2. SDK 调用阻塞时缓存状态仍可读取；观测变旧可见。慢日志不阻塞采集，日志异常可见；每次成功启动使用新的监控实例，已进入的旧回调只影响旧实例。满队列在 Pop 前采样，排空不会擦除疑似满证据。优先级申请失败不清历史诊断，成功重启才重置。
3. 首次完整运行有一个原有 20 ms 运动测试失败：它未确认假 I/O 已进入阻塞就断言轴锁必须仍持有。独立复核源码与现场日志后，只修改测试/Fake，加入进入握手并在 finally 释放；保留第二次短超时和无新读取断言，未改生产运动控制逻辑。最终两套测试通过。
4. 新布局首次构建暴露编辑器 DTO 命名空间解析错误，已修正生成源并重生；前置判定显式匹配底层 `Models.Operations.RequestStateSetting`。非 OP 前置告警及 OP 正常情况均有测试。固定前置提示不再显示两次相同动作；七个组合仍显示组与具体操作。旧 28 入口按新目录清理，其中三个组名为相似操作组复用，另外 25 个旧名不再公开；330 个具体旧生成文件清理前留有逐文件备份。
5. 早期集成测试的断言类型和测试入口尚未就绪造成编译失败，不作行为 RED 证据；最终对应测试均已实际运行。助手探针首次更新曾保留旧步骤数量退出条件，已改为 97，重新构建并通过。

## 未验证范围

未连接 HPCIe-2E、驱动器或现场 ENI，未测试 250/500 μs、DC 同步、实际 FIFO 覆盖、原生长期负载、物理停止或完整在线助手。Windows 提升共享线程优先级不保证硬实时；SDK 阻塞也会延迟队列观测及告警。既有后台池的慢发布/日志可延迟该池的其它发布；原生调用与已进入的宿主回调不能被软期限强制撤回。未做覆盖率测量。硬件功能和现场时序不能以本记录认证。
