# 12项修复、插件树与AO交付验收

## 范围和来源

- L3，按用户批准的方案实施。保留既有工作区修改，未重置仓库、未提交/推送，也未替换正在运行的平台目录。
- 手册远端 master 与本地 blob 同为 `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`，按 3.4.0 手册及 SDK 1.2.8 的执行、配置、编辑器和生命周期契约实现。
- 原问题单：[12项审查报告](2026-10-24-plugin-lifecycle-blocking-audit.md)；需求：[能力与树方案](../requirements/2026-10-24-plugin-capability-taxonomy.md)；过程：[分阶段计划](../plans/2026-10-24-plugin-remediation-ao-delivery.md)。原审查是历史基线，不把其中未修结论当作本次最终源码。
- 最终输出：`artifacts/Release/2026-10-24-remediation-ao/Plugins`。本次软件范围已完成实施、独立复审及本地编译；硬件与完整宿主验收仍未执行，不能据此认定所有插件绝无问题。

## 逐项实现与验证对应

| 编号 | 最终行为 | 定向证据位置（本仓库 artifacts/remediation-ao-20261024/） |
|---|---|---|
| LCA-01 | Shared PendingIoGate 在实际 worker 提交前复查原令牌、原单调截止与关闭/隔离；真正调用未退不释放门 | `shared/shared-gate-red.trx` → `shared/shared-gate-green.trx`；延迟入口屏障与已提交调用分别验证 |
| LCA-02 | CAN raw / UDS / XCP 走实际 owner 与完整协议事务预算，预取消零发送、复合 XCP 不能交错；晚响应不能归入下一事务 | `can-lin/can-lca-red.trx`、协议用例；最终集成追加 UDS 响应窗口/原生软等待竞争确定性测试，见 CAN 最新回执 |
| LCA-03 | 移除所有 DAQ 步骤的冗余全局枚举及永久失败缓存；实际 SDK 建立/访问在 owner 中，Stop 不重新枚举 | `nidaq/driver-red.trx` → `driver-green.trx`，实际 TaskStop 执行器 + 替身 owner |
| LCA-04 | DAQ 导出新增 ExportTimeoutMs；OPC 消费/导出新增总期限；实际任务持门至退出，迟到任务不得写变量，成功 CSV 迟到消费一次；失败部分 CSV 路径隔离 | NI 文件锁/轮转/发布回归；OPC `red-opc`、`red-csv-partial-confirmed`、`red-collector-lifetime` → `green-opc-review-fixes` |
| LCA-05 | 已故障但真实静止的监测服务继续执行实际关闭并保留历史错误；未知发送/PartialCycle 不放行 | CAN 故障停服 RED/GREEN 与反例保护 |
| LCA-06 | 再次确认精确旧 owner 已释放后首次重开可继续，历史错误不假装仍占用；未释放仍按预算等待/隔离 | `shared/shared-retirement-red.trx` → green；`can-lin/can-reopen-red.trx` → green |
| LCA-07 | LIN Sleep 接入实际 TimeoutMs/当前令牌，取消后不返回 Completed，原生未退出不释放 | `can-lin/lin-sleep-red.trx` → green，两种实际执行器交错 |
| LCA-08 | DoIP 请求 TimeoutMs 覆盖事务排队/ACK/完整响应，不串用连接默认期限；排队超时不误关活动事务 | Ethernet 实际 TCP loopback / gated 回归 |
| LCA-09 | SOME/IP DNS、连接和发送采用预算内异步入口，取消/内部超时分开 | Ethernet DNS 注入与真实 localhost UDP/loopback |
| LCA-10 | UDP / SOME/IP FAF 新增 SendTimeoutMs，Setting/Description/VM+UI/Validate 及旧默认闭环 | Ethernet 配置、发送与真实 WPF 即时卸载/刷新测试 |
| LCA-11 | CANopen 基础发送新增 SendTimeoutMs，通用周期停止新增 TimeoutMs；用户预算实际生效 | `can-lin/can-send-budget-red.trx` → green、实际 NMT/PDO/SYNC/Stop 和页面测试 |
| LCA-12 | 串口 Read/Query 的上下文和设置解析在错误边界内，损坏设置返回中文 ErrorInfo；旧 v1 map 保持兼容 | `opc-serial/red-serial-confirmed` → `green-serial-full` |

上述修复不使用统一固定超时，不清除真实 pending 或未知发送。内部超时为 Error，调用方中断为 Aborted；正常返回 Completed 不等于设备物理状态合格。

## 独立审查和追加修复

非对应实现者按 C# 与异常传播方法读取源码、调用方、测试和实际 TRX，回执分别在 `artifacts/remediation-ao-20261024/review-{shared-nidaq,can-lin,ethernet,opc-serial,ao}.md`。

- Shared/NI：文件轮转日志返回后再次校验原预算；标量发布前检查原调用方/owner，已修复复测。
- Ethernet：响应日志返回后重新检查原调用方取消，禁止迟到发布旧变量；两项真实 RED 后全65通过。
- OPC：自然采集结束不等于关联消费者关闭，连接保留至显式真实 CloseCompletion；部分追加失败跨 owner 同规范路径禁止盲重试，保留 FIFO。均补真实 RED 后全58通过并复审关闭。
- AO：配置取消不得额外用 None 等待清理并变成 Error；准备阶段失败与真正电压写未知区分。各一项真实 RED 后修复，独立复审关闭。
- 最终 CAN 集成暴露 UDS 响应窗口与 raw 软期限竞争，首次全量170/171失败已保留；补充确定性 RED 后区分接收窗口超时/总事务预算/发送失败/调用方取消，实际未退出 I/O 仍隔离，最终177/177并独立复审关闭。原20ms用例未放宽，8项定向连续三轮通过。

## 树分层与兼容性

- 仅按批准的优先方案改38项 DisplayName：CiA402 18项（Session/State/Motion/Target/Feedback）、CANopen 14项（SDO/PDO/Network/Service/Cycle）、TCP/UDP 6项（Ethernet_TCP/Ethernet_UDP）。
- 保留全部118个旧 StepTypeId、Category 与原设置身份；新增 `NiDaq.AoConfig` / `NiDaq.AoWriteSingle`，总120步骤、120独立编辑器。16项可选命名调整未实施，不改变其已有树/身份。
- 显示名映射、旧ID全集、无路径碰撞和 CiA402 私有 CAN 相同哈希由最终元数据校验；不以源码正则替代实际装载结果。完整宿主搜索/收藏展示尚未运行。

## AO 新增能力与边界

- 两步骤均有独立 Setting / Plugin / Executor / EditorPlugin / VM / XAML、生成图标、完整参数说明及编辑校验；集合子项实现通知，卸载/刷新 Flush 原步骤。
- 同设备/模块、每行一个 AO 通道，1~256路。按配置行序复制数值，支持数值或数值集合；全部值有限且量程内，数量必须一致，不截断或补零。
- Config 不调用电压写；TaskStart 显式 Start 后写已复制初值；WriteSingle 不自动启动。新默认配置无通道/初值、StopPolicy=Unspecified，必须用户明确填写。
- DeviceDefined 不保证保持、归零或高阻；WriteFinalValues 要求显式终值。中断先冻结目标、等真实 writer 退出，再处理终值/Stop/Dispose。半启动也收尾，成功终值不重复写；未提交准备失败可重试，真正终值未知禁盲重发。
- CleanupTimeoutMs 与 TaskStop 的等待 TimeoutMs 分离。调用方可以限时退出，但驱动若永久不退，旧任务会继续隔离，不能强关句柄承诺强制重跑。
- 已验证替身 core、实际 Start→Write→Stop 执行器、两个实际 WPF 页面绑定/即时卸载保存。通道子项经 VM 实体修改，不冒充每个 DataGrid 键盘操作、全 DPI 或完整平台验收。
- 没有波形、CO、自动重连、自动恢复输出或实际序列修改；SDK 返回不是实测电压或电气安全证明。

## ECC 能力检查实际结果

| 方法 | 实际执行结果 |
|---|---|
| planner / architect | 保存阶段、依赖、真实任务所有权、AO 输出处置、兼容与回退决策；同构建共享类型原子修改，不额外引入契约框架 |
| tdd-workflow / csharp-testing | 保留原失败探针及新增确定性 RED/GREEN；覆盖延迟入口、取消、超时、已释放历史错误、导出、半启动与真实页面 |
| C# reviewer / silent-failure-hunter | 三个原生子 Agent 交叉只读审查，主 Agent 核实、修复、重测；未将作者自测称为独立审查 |
| security-review | 检查通道/数值有限性、整个向量预检、固定路径生成及 CSV 部分提交隔离；未新增凭据/权限/远程服务，不操作设备；不宣称全依赖漏洞审计 |
| windows-desktop-e2e 方法 | 使用现有同 Dispatcher WPF 组件测试实际绑定/布局/保存；完整宿主不可用，明确降级，不以构建代替 UI 运行 |
| verification-loop | 最终20项目重建、9测试项目、UTF-8/差异、严格装载/依赖/版本/哈希/PE-PDB源码核对全部通过；具体结果如下 |

## 主要修改文件

- 公共生命周期：`Shared/Runtime/PendingIoGate.cs`、`RetiringResources.cs`。
- CAN/LIN：`CAN/CanPlugin/Runtime/{CanProtocolTransaction,CanConnectionResource,CanopenCyclicService}.cs`、`UDS/UdsClient.cs`、`UDS/IsoTp/IsoTpTransport.cs`、`XCP/XcpClient.cs`；`LIN/LinPlugin/Executors/LinSleepExecutor.cs`及对应发送/停止参数模型和 UI。
- 数据与退出：`NiDaq/NiDaqPlugin/Helpers/{DaqTaskResource,DaqTaskFactory,DaqFileWriter,DaqResultPublisher}.cs`；`OpcUa/OpcUaPlugin/Helpers/{OpcUaConsumptionQueue,OpcUaCsvExport,OpcUaDataAcqTask,OpcUaConnectionResource}.cs`及读取执行器/配置；串口 Read/Query 执行器。
- Ethernet：`Ethernet/EthernetPlugin/DoIP/{DoipClient.cs,Executors/DoipDiagRequestExecutor.cs}`、`SomeIP/Executors`和`Runtime`预算/异步网络帮助类，以及 UDP/FAF 配置 UI。
- AO：`NiDaqAoConfigPlugin.cs`、`NiDaqAoWriteSinglePlugin.cs`、`Models/NiDaqAo*.cs`、`Helpers/{AoOutputPlan,AnalogOutputSession}.cs`及两个独立 Editor/VM/XAML/图标/生命周期校验；`tools/Icons/New-DaqIconStyles.ps1`。
- 树与交付：CiA402 18个插件、CANopen插件描述、Ethernet TCP/UDP描述；20个csproj、10份CHANGELOG和4份README；新增`tools/Verify-PluginBinarySources.ps1`。完整清单/哈希以 compatibility-receipt 和工作区差异为准，未将原有用户修改冒认为本次独立新增。

## 最终集成结果

实际命令（均在仓库根目录运行，输出不指向实际平台）：

```powershell
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Test -OutputRoot artifacts/Release/2026-10-24-remediation-ao
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Package -OutputRoot artifacts/Release/2026-10-24-remediation-ao
pwsh -NoProfile -File tools/Verify-Plugins.ps1 -Mode Validate -OutputRoot artifacts/Release/2026-10-24-remediation-ao
pwsh -NoProfile -File tools/Verify-PluginBinarySources.ps1 -OutputRoot artifacts/Release/2026-10-24-remediation-ao
pwsh -NoProfile -File tools/Verify-PluginSources.ps1
node artifacts/remediation-ao-20261024/verify-final.mjs
git diff --check
```

| 测试项目 | 最后源码状态下通过 / 总数 |
|---|---:|
| CAN | 177 / 177 |
| CiA402 | 208 / 208 |
| LIN | 29 / 29 |
| Modbus | 21 / 21 |
| NiDaq | 47 / 47 |
| OpcUa | 58 / 58 |
| SerialPort | 16 / 16 |
| VISA | 29 / 29 |
| Ethernet / Http / Shared综合 | 65 / 65 |
| **合计** | **650 / 650，失败0、跳过0** |

- 9份TRX及汇总：`artifacts/Release/2026-10-24-remediation-ao/verification/test-summary.json`。首轮集成失败另保留于`artifacts/remediation-ao-20261024/final-can-integration-first-red.trx`，并非覆盖失败后假装从未发生。
- 20个 core/UI 项目全部重新构建，0错误。CAN 的既有 `CanopenExecutorHelper.cs:100` CS8601 nullable 警告仍存在（编译CAN及CiA402依赖时重复出现），不宣称0警告；不是本次新增错误。
- 10个部署目录严格装载、120个步骤各唯一编辑器、版本一致、Windows RID的System.IO.Ports实现、无测试DLL污染检查通过。CiA402私有CAN与CAN模块DLL相同SHA-256。
- 全118旧ID保留、38选定显示名符合映射、仅新增2 AO、无显示名冲突通过；Category源常量/字面值保持。说明表格从实际装载元数据刷新，保留手写正文；此前验证脚本对常量/对齐空白的误判已修正，是工具设置问题而非产品回归。
- PE/PDB来源核对：30个程序集、1241条非生成C#源码文档哈希匹配；不加载厂商原生驱动。1102个源码/项目/XAML文件的最终聚合指纹：`a69ed547a5f9b1c69b80f0eca2593750e942407827662d3c8e28144e89494da4`。
- UTF-8、C#无BOM、120个独立Editor描述、状态范式与差异检查通过（1216个文本文件）。这些静态检查不替代语义或设备验收。
- `build-manifest.json`记录全部部署文件哈希；`verification/delivery-source-receipt.json`记录AssemblyVersion、FileVersion、MVID和源码对应；`verification/compatibility-receipt.json`记录ID/映射/私有依赖及源码指纹。
- 当前CAN身份：Assembly/File=`1.0.35.0`，MVID=`a8d4b11f-ba0e-43da-95b0-483e4695d181`，SHA-256=`0ee27cb7d4100eb0cf6c49ffb9be8676260a27cb15828e48745d37609524f5a1`。这不是对现场已加载DLL的读取认证。

## 安装、回退与剩余风险

- 关闭平台后备份原 Plugins，替换本交付中相应模块的**整个目录**再启动；不要混用新旧 core/UI 或 CiA402 私有 CAN。没有自动覆盖当前平台。
- 版本按原补丁序列递增：CAN1.0.35、CiA4021.0.7、LIN/SerialPort1.0.29，其他六模块1.0.21。实际二进制身份以最终 manifest / delivery-source-receipt 为准。
- 回退使用备份的完整目录和兼容序列版本；旧插件不认识两个新AO步骤，不能仅回退 DLL 后继续执行新步骤。软件回退不能撤销已产生的硬件输出。
- 本机 NI-XNET / NI-DAQmx 26.5、原生 DLL 与 nixnet.h 已只读确认，**没有打开真实设备、输出电压、发送设备 CAN/LIN 或驱动电机**。
- 未验证：完整平台停止→立即重跑→退出、真实厂商SDK的极端挂起/Abort/Stop/Clear时延、总线顺序/周期/帧时钟、实测电压及外设安全。损坏驱动永不返回时必须保留引用/隔离，不能保证强制重跑。
- OPC CSV Unknown 隔离仅在当前插件进程内，重启不证明文件完整；硬链接/符号链接路径别名不保证识别。CSV 已部分写入不可假称回滚；需核查 Sequence 并使用新路径重试。
- 原报告中的非阻断条件风险仍保留：外部 EtherCAT 适配器释放能力、未来 VISA 消费者的慢取消回调、OPC 旧 CSV-only 配置说明与编辑器限制差异。它们不是本次已关闭12项的现场复现，也不应据本次测试宣称所有代码绝无缺陷。
- 覆盖率未测量，未做全依赖漏洞扫描；未运行的检查不记为通过。
