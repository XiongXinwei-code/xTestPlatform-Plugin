# 全插件退出机制修复与三轮勘误

任务等级：L3。范围：CAN、CiA402、Ethernet、HTTP、LIN、Modbus、NI-DAQ、OPC UA、SerialPort、VISA，及其私有共享执行代码。用户已明确先做软件验证；本轮未连接设备、写入真实输出、运行设备序列或替换平台部署目录。

本轮已确认的问题均已修复并经非作者读回，三轮软件勘误、冻结后的完整回归及独立插件打包已完成。该结论限定于下面列明的源码和软件验证范围，不宣称未知缺陷归零，也不代表现场设备或实际平台窗口已经验收。

## 1. BUG 的原理

问题涉及三个不同生命周期，却曾只用一个调用方 token 或超时代表它们：

1. 前台步骤正在等待结果；
2. 连接、轴、采集或周期任务所属的 SDK 执行作用域；
3. 驱动真实 I/O、原生取消回调及句柄释放。

SDK 中断/退出调试可以回收 Execution 资源而不取消当前 step token。旧代码的 closing 标志会禁止新请求，却不会唤醒已排队或正在等结果的调用；还没有登记 owner 的表达式、初始化及纯延时更收不到通知。等待可能持续原配置的几十秒或无限期限。另一方面，把软等待结束当作 native 已退出，提前清空任务/物理占用，又会使旧代码与第一次重跑争用同一通道。

因此不能用固定 5 秒、清空占用表、忽略停止错误或重新绑定旧任务解决。必须让前台及时退出，同时保留实际 I/O、回调、发送确认和资源所有权，直至它们真实结束。真实驱动永不退出时，软件应返回明确隔离错误，不能承诺“立即重跑一定成功”。

上一轮 1946 项软件测试没有充分证明这一条件：部分测试先放行底层屏障，再检查旧步骤结束；另有 HTTP 用例明确允许旧前台继续等待。它们证明了不误释放和新执行不被覆盖，没有证明旧前台在 native 尚未退出时也能及时结束。本轮增加相反顺序的屏障测试，并保留实际 RED。

## 2. 统一机制与兼容边界

| 机制 | 修改后的行为 | 为什么需要 |
| --- | --- | --- |
| OwnerStopSignal | 单独唤醒前台及撤销排队；不直接取消 native、不归还实际 I/O gate | 关闭标志本身不会唤醒等待；排队撤销也不能留下偷取 permit 的迟到 waiter |
| PendingIoGate | owner 退出立即冻结准入；原始任务继续占有 gate；自动收尾诊断在后台 | 前台结束不等于实际退出，已完成失败也不能在 SDK 回收线程内联执行日志 |
| ExecutionEpoch | 记录 SDK 代次并通知回收，异步续体保留原代次 | 旧表达式/初始化不能在中断后捕获下一运行的新资源继续工作 |
| ExecutionScopedExecutor | 所有实际步骤工厂统一进入执行观察边界；原 caller、SDK 回收、明确 Error 分别处理 | 不能要求 SDK 必须先清理，也不能把内部超时当用户取消 |
| StepLifetime / Own | 仅冻结本步骤新 owner 或捕获的精确代次，成功后解除短期关联 | 既要在返回 Aborted 前请求收尾，又不能使已成功连接/轴/采集继承旧步骤令牌寿命 |
| OwnOperation | 只在事务准入后登记快冻结，结果/取消处理后解除 | 排队失败不能误停别的事务；已提交的半双工事务不能在 Aborted 返回时仍报告可复用 |
| 原生取消桥 | 请求取消快速返回，实际事务同时等待 native 与取消回调退出；双失败保留 | caller.Cancel 不能同步进入任意驱动回调，native 本体先返回也不能越过仍运行的回调释放句柄 |

不修改 SDK 公共接口、序列字段/枚举、120 个步骤身份或协议组包。所有等待仍使用用户配置的预算；观察器没有新加固定业务超时。CiA402 只管理轴/节点，不关闭父 CAN；CAN 受控所有者停机例外、PartialCycle 和未知发送隔离继续保留。ACK、任务退出、CSP 点表结束均不等于物理停稳。

## 3. 各插件的实际修改

| 插件 | 重点修复 |
| --- | --- |
| CAN | Open/Read/Write/确认能力预检、CANopen raw/受控路由和协议门的退出通知；原生读取取消回调真实 join；重复 Dispose 的失败诊断不内联阻塞；后台停机保留精确运动代次和监测 |
| CiA402 | 前台观察与实际 action/count 分离；协调传输等待及取消回调归属；拒绝第二控制操作不误终止第一任务，保留首次终止原因；释放轴不越过活动操作 |
| LIN | 先冻结 gate 再停任务；公共读写作为前台观察，周期 worker 保留真实发送退出语义；原生接收取消桥不阻塞回收/调用方线程 |
| Ethernet | pool 与每次 Open 独立退出；旧工厂仍精确持有迟到句柄；同 pool 取消新连接不影响 keep 连接；DoIP 已准入事务冻结先于中断返回 |
| HTTP | 建立/关闭排队按 epoch 分域；请求/证书初始化的观察可退出，真实任务与迟到客户端仍被精确释放；不变更认证和脱敏规则 |
| Modbus、SerialPort | 活动/排队/建立中的等待退出；打开补偿不重新等待未退 native；真实 CloseCompletion 成功后按精确 owner 移除，迟到查询不能删新连接 |
| NI-DAQ | 无限读取也可结束前台等待，Abort/释放仍归原 native；先 freeze 再 Abort，新建及临时任务登记本步 Own，不做 DeviceReset |
| OPC UA | Stop/Freeze 唤醒活动及排队消费；CSV 实际提交及取消回调保留，错误聚合不覆盖；采集 Task.Run 前挂快停止，成功启动后旧步骤取消不杀采集或 Session |
| VISA | caller/owner/期限异步取消桥；原生与实际回调均结束后释放；无限读取、排队和同物理资源重开仍遵守原 owner 归属 |

## 4. 三轮勘误与证据

第一轮为机制复现。实际 RED 文件保存在 `artifacts/lifecycle-mechanism-20261026/`，包括 Shared 3/4 失败、执行作用域延时 2/2 失败、原 caller 与明确 Error 2/2 失败，以及各模块的 owner stop、取消回调、关闭日志及打开补偿。编译错误与夹具修正不计产品缺陷复现。

第二轮为实现和非作者复审，已额外修复：明确 Error + 快冻结失败时丢失原错误；构造半完成 owner 被提前释放；把执行级 pool 当某一读取步骤私有资源导致恢复失败；后台 CSV 取消错误被覆盖；CiA402 拒绝并发控制时误终止首任务；取消回调晚于 native 返回时的真实释放边界；晚到 Error 恰在 finally 前完成的诊断交错。

最后的非作者收口还关闭了 OPC 取消 Task 快照后新增回调的交错、VISA 日志拖住已结束 I/O 的假占用、长批次操作登记累积，以及清理任务继承已退休步骤上下文的风险。CAN 的普通步骤围栏严格区分受控 owner 命令，启动尚未成功返回时取消也会请求已配置收尾；该例外不放宽未知发送、PartialCycle 或停机预算。CiA 有效清理 RED/GREEN 只认 corrected 写控制字夹具，先前误观察 Read 的夹具记录不计证据。

新屏障同时检查前台及时返回、native/回调尚未退出时释放次数为 0、精确 owner/代次仍保留、放行后只释放一次、第一次重跑使用新结果，以及成功资源不受旧步骤 token 影响。模拟并行阻塞造成的测试线程池饥饿与产品缺陷分开：真实 Modbus 回环关闭的 500 ms 预算不变，该时序用例归入非并行隔离集合，单独复测通过后仍须参加最终完整回归。

第三轮在干净产品提交 `61b5fa8e68ba86948c22ad83b548a033aaac9810` 冻结后执行；九套完整回归全部通过，十核心/十 UI 完整构建通过，120 步骤与工厂/编辑器、PE/PDB 来源和依赖指纹均通过。与前一交付相比增加 79 项有效软件回归（1946 → 2025），没有跳过测试。此后只更新本报告和计划，不改产品代码或重写已编译源码身份。

## 5. 基线、阅读范围与能力检查

- 起点 `260f749cd6d78af9ecf2fcfd9b3a1d30ab6e4273`；工作分支 `codex/plugin-lifecycle-mechanism-20261026`；旧 `develop-backup-20261024` 保留在 `54ef5f5f38d64813fb409b1d000b966a8a1a2949`。不推送远端。
- `origin/master` 为 `5351a0a9d66594455b292fe65308303488a07537`；本地/远端开发手册 blob 均 `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`。依据手册的设置、状态、错误责任及资源规则实现。
- 复用前轮已逐文件审阅的 1021 项自有产品源码基线，对未变文件以哈希核同；本轮重新读十模块生命周期入口、资源/后台任务、修改调用链和测试，逐项审当前差异。自动哈希不冒充新一轮人工逐文件阅读。
- 最终产品源码清单为 1027 项：862 项与前轮全文记录哈希相同，159 项差异/调用链复核，6 项新增源码。明细保存在 `artifacts/lifecycle-mechanism-20261026/product-source-baseline-comparison.json`，不包含不透明厂商 DLL 的内部实现。
- ECC planner/architect：已执行职责/调用流、兼容性、阶段依赖及失败恢复分析；计划见同日 `docs/plans/`。不引入新 SDK 契约、MCP、自动 hook 或学习写入。
- ECC TDD / C# reviewer / silent-failure-hunter：保留屏障失败、修复、模块回归并检查异常聚合、未知 I/O、native callback join 与误释放。三组非作者只读交叉审查实际读取相关源码，不把自审称独立审查。
- 安全检查：本轮不增加设备命令、凭据字段、文件写入或网络权限；通用晚到诊断不输出 payload、URL、Token、密钥或外部异常文本。原具体资源仍保留自身收尾详情。
- verification-loop：最终冻结后按下节结果验收。没有安全宿主工程，不执行完整平台调试/运行切换 E2E；没有真实设备时不把 WPF 组件、SDK registry、fake 或回环证据当硬件通过。

## 6. 最终验收、编译与使用边界

最终输出：`artifacts/Release/2026-10-26-lifecycle-mechanism/Plugins`，十个可整体替换的插件目录；目标 `net8.0-windows/win-x64`。没有覆盖前一交付或平台目录，没有推送远端。包内 543 个文件、432,939,208 字节，不含测试程序集；核心/UI/依赖/模块文档均已核对。

实际命令与证据：

| 检查 | 命令/记录 | 结果 |
| --- | --- | --- |
| 最终完整回归 | `tools/Verify-Plugins.ps1 -Mode Test -OutputRoot artifacts/Release/2026-10-26-lifecycle-mechanism`；`round3-all-tests.log`、九份新 TRX | 9 套，2025/2025，通过；0 失败、0 跳过 |
| Release 插件编译 | 同一工具 `-Mode Package`；`round3-package.log`、十份 build.log | 十核心、十 UI，0 警告、0 错误 |
| 步骤/工厂/依赖 | 包内 StepInfoExtractor `--strict`，每个工厂返回独立作用域执行器 | 10 模块、120 个 ID、编辑器唯一、与上一交付身份/显示路径一致 |
| UTF-8 与静态范式 | `tools/Verify-PluginSources.ps1` | 1394 个文本文件、120 个独立编辑器；C# 无 BOM，0 问题 |
| 交付合同 | `tools/Verify-PluginDelivery.ps1 -OutputRoot ... -BaselineRoot artifacts/Release/2026-10-25-lifecycle-followup` | 10 模块、120 步骤、9 套/2025；两份私有 CAN 同 SHA-256 |
| PE/PDB 对应 | `tools/Verify-PluginBinarySources.ps1 -OutputRoot ...` | 30 个程序集配对正确，1443 次非生成 C# 文档引用与当前文件匹配 |
| 最终差异 | `git diff --check`，本地/远端手册 blob 再核同 | 通过；包构建时工作区干净，manifest 明确 `sourceIncludesUncommittedChanges=false` |

最终完整套数：

| 测试项目 | 通过 |
| --- | ---: |
| CAN | 567 |
| CiA402 | 244 |
| LIN | 121 |
| Modbus | 173 |
| NI-DAQ | 118 |
| OPC UA | 195 |
| SerialPort | 84 |
| VISA | 92 |
| Ethernet/HTTP 跨插件回归 | 431 |
| 合计 | 2025 |

产物版本（核心/UI 同版）：CAN `1.0.39`，CiA402 `1.0.11`，LIN/SerialPort `1.0.33`，Modbus `1.0.24`，Ethernet/HTTP/NI-DAQ/OPC UA/VISA `1.0.25`。

CAN 指纹：AssemblyVersion/FileVersion `1.0.39.0`；MVID `8ea0e138-194d-4f3a-ab99-f91bb1d07fca`；SHA-256 `400d945df71e27b45fe6b44e1fd83874a52540a6d8676309758317e6179c181b`。独立 CAN 与 CiA402 内的私有 CAN 均为该文件。完整元数据分别在交付 `verification/delivery-contract-receipt.json`、`delivery-source-receipt.json` 和 `build-manifest.json`；后者 SHA-256 为 `fbe8dbea915befb63e1bfd10fe9f89b00c28f0d7b7c7d4a13910238390e25d01`。

与云端 master 的完整文件差异见 [清单](2026-10-26-mechanism-vs-master-files.csv)：1141 项，其中 211 项本轮增量、930 项前轮已有；不把以前的 AO 新步骤、树分层和历史修复算成本轮新写，也不把清单/本报告自身循环计入。产品提交为 `61b5fa8e`，最终验收文档另存同一分支；原备份分支未移动。

使用时先完全退出平台，再替换相应整套目录；CAN 和 CiA402 使用同批私有依赖。不要只拷主 DLL 或混用新旧 UI/依赖；本轮未安装驱动或代用户连接设备。交付根目录另有使用说明。

剩余现场验收：实际平台“中途中止后首重跑”和“退出调试窗后重新进入”；NI/ZLG/DAQ/VISA 等真实驱动停止与回调时序；CAN 总线顺序/确认、实际轴停稳、OPC 设备断链及重连策略。真实 I/O 或回调永不退出时仍保持隔离并报告，不能为使重开成功而强行放行通道。
