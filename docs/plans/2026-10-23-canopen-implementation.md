# CAN / CANopen 通信层优化实施记录

日期：2026-10-23。任务等级 L3（共享接收、原生 I/O、周期发送、取消与关闭并发）。

状态：本次通信层软件实现、模拟回归与编译完成；真实厂商发送确认与可信采集时钟映射尚未实现/合格验证。不能宣称完整生产运动控制能力已交付。

阶段：
1. 固定连接所有者、接收元数据和可选能力契约。
2. 实现持续共享路由与非消费式最新帧快照，保留旧 FIFO。
3. 实现单执行单元 RPDO→SYNC、原子输入更新及故障状态。
4. 接入停止/关闭屏障和旧周期任务跟踪，防止重复启动与未退出资源释放。
5. 新增步骤及编辑器，验证序列化、配置保存和错误输出。
6. 执行 CAN 核心/UI 构建、定向并发测试、CiA402 回归、代码审查和 UTF-8 检查。

边界：目标值及字节组包由序列提供；不引入后台活跃序列上下文求值，不改变设备 PDO/NMT/使能。
厂商尚无经核实的发送完成契约时，强保证必须明确拒绝；时间域无法映射时输出 Unknown，不用出队时间伪造新鲜度。
现有 CiA402 未提交修改属于用户已有工作，不在本次中改写。

## 范围与实施决策

- 仅修改 CAN 核心/UI、对应测试和文档。保留工作区已有 CiA402 未提交修改，不提交、不推送、不改写 CiA402 源码。
- 主规划使用 ECC planner，架构使用 architect：连接持有唯一接收入口、所有关联任务和关闭屏障；快照观察与 FIFO 消费分离；每连接单有序发送所有者。
- 原始 CAN/UDS/XCP 模式继续保留；启动受控服务后禁止原始读者与 UDS/XCP 混入。需关闭重开才能换回原始模式，不能静默退化成抢读。
- 目标计算保留在序列，完整 RPDO 字节快照一次发布；后台固定一次输入版本后按 RPDO 列表→SYNC 执行，不持有活跃执行上下文求值。这是原方案“计算→发送”契约的收敛，避免后台表达式跨步骤生命周期。
- 采用同一构建内共享 C# 类型与既有 SDK 资源表，无独立协议生成器；输入输出通过新增 StepType 和 JSON string 快照。公共 API 仅增加可选能力与元数据，不移除 ICanAdapter 原接口。
- 强模式默认要求厂商发送确认；当前真实适配器均不实现，启动前报 UnsupportedGuarantee。显式关闭确认只供隔离实验，不能保证总线顺序或可确认停止；不因完成软件任务而伪造硬件能力。
- 源时间无法映射则 Quality=TimeUnknown、Valid=false。IngressMonotonicTicks 只是观察时间，不给旧帧续期。只报告可取得的统计，未知为 null。
- 停止等待真实工作/取消回调退出；部分周期继续保留 SYNC/节点占用。已完成实际关闭但历史后台故障存在时返回 Error 并移除已关闭资源；未确认退出或待发未知时保留句柄与占用。

## 阶段依赖与结果

| 阶段 | 依赖和预期 | 实际结果 |
| --- | --- | --- |
| 1 契约/所有者 | 先确定时间与发送能力边界 | CanRuntimeContracts、CanConnectionResource、CanChannelIdentity；现有资源键兼容，命名和物理通道保护 |
| 2 接收 | 依赖连接唯一入口 | CanReceiveRouter；每 ID 64 帧 FIFO、非消费快照、后台错误；SDO 和 TPDO 使用共享入口 |
| 3 周期发送 | 依赖所有权与提交确认契约 | 单任务、整批发送门、不可变输入发布、迟到/输入过期停止；严格厂商能力未落地，不宣称硬件完成 |
| 4 生命周期 | 依赖任务/原生 I/O 跟踪 | 实际退出 join、代次检查、连接内具名任务、部分周期保护、旧 Close 身份捕获 |
| 5 步骤/UI | 依赖稳定契约 | 六个独立 StepType 与编辑器，列表保存、原始字节、快照 JSON；STA WPF 组件运行测试 |
| 6 验证/交付 | 集成最终源码 | CAN 32 项、CiA402 204 项、核心/UI Release 编译、独立复审、UTF-8/差异检查；实卡与完整宿主 E2E 未执行 |

## ECC 能力选择与实际执行

| 能力 | 触发原因 | 实际检查/结论 |
| --- | --- | --- |
| planner | L3 范围与阶段依赖 | 先有设计，再用户授权实施；本记录维护阶段、风险、验证和交付边界 |
| architect | 连接所有者/共享路由/公共类型 | 核对 adapter→resource→CANopen/UDS/XCP 调用链；保留原始模式，不复制 NI 原生接收泵 |
| contract-first 原则 | 提供方/步骤/UI 契约演进 | 本次同一构建共享类型，不额外加载契约工具；设置序列化、六个 StepType 唯一性、快照 JSON 测试 |
| tdd-workflow | 状态/异常/退出核心行为 | 初始实施使用回归保护；审查发现的部分周期、吞错、接收故障关闭保留了真实 RED→GREEN，不把所有测试声称为测试先行 |
| csharp-reviewer | C# 并发、任务与资源释放 | 主 Agent 差异自查 + 原生独立只读子 Agent can_review，阅读相关源码/调用链，不执行设备动作；最终复审无剩余发现 |
| silent-failure-hunter | 取消、超时、后台错误传播 | 审查捕获取消、晚到原生异常、关闭历史错误、CycleStop 接收故障；已修复并定向重测 |
| security-review | 序列配置/字节输入和 SDK 表达式边界 | 标准 ID≤0x7FF、DLC≤8、最多128路、重复ID拒绝、输入完整发布、复制防突变、队列有界；未增加凭据/进程/网络服务。定向敏感 API 搜索无新增命中；不是全仓安全认证 |
| windows-desktop-e2e | WPF 编辑器 | 已读方法；采用实际 STA WPF 控件布局、DataGrid 单元格编辑、保存/重载验证。未启动真实测试平台执行全宿主 UIA/E2E，不安装额外工具，不将组件测试等同全流程 |
| verification-loop | L3 最终验证 | 核心/UI构建、CAN与CiA402测试、新源格式、UTF-8、差异及版本/包检查；未测覆盖率，不宣称阈值达标 |

本次没有自动 Hooks、持续学习、记忆写入或额外 MCP 安装。独立审查是只读角色，实施与集成仍由主 Agent 完成。

## 独立审查与修复闭环

| 问题/触发 | 修复 | 验证与复审 |
| --- | --- | --- |
| 两个旧 Stop 等待者与新 Start 交错，旧确认释放新代次 | Stop 原子捕获 worker，ConfirmExit/超时核对身份 | 独立复审确认代次检查；未声称穷举所有线程交错 |
| 旧 generic Stop 删除新同名任务 | Remove 前核对 job 实例 | 源码调用链复审；连接内任务/真实退出测试 |
| 排队旧 Close 关闭后来新连接 | 锁前捕获 expected，锁内核对实例；生命周期锁使用 OrdinalIgnoreCase | LifecycleGuardTests 两项：名称大小写共锁、排队旧请求不关闭替代实例；复审通过 |
| 部分 RPDO 已送达但未发 SYNC，被误报可重启 | PartialCycle 保留节点/SYNC占用；逐帧检查源CTS，避免CancelAsync传播窗口 | partial-cycle-red.trx 真实失败；green通过；后续全量发现回调时序窗口并修复、重测通过 |
| 同一物理端口的数值/型号别名绕过重复打开 | 按各适配器原解析器规范化 key | 5组 Theory（Kvaser/Vector/TOSUN/PEAK/ZLG）与独立复审通过；NI外部配置别名不猜测 |
| 停止/截止时间后晚到的原生错误被吞或被 OCE 覆盖 | join 后保留原始异常，外层不覆盖已有根因 | async-errors-red.trx 两项均失败，修复后通过；复审确认 deadline/显式取消两分支 |
| 直接 CAN_Close 丢失后台发送/接收错误 | 实际释放后汇总历史错误，IsClosed区分可移除资源；未退出仍保留 | generic关闭、接收关闭测试，close-errors-red.trx有失败证据；复审通过 |
| CycleStop 只看发送故障，漏报导致停止的接收错误 | 同时检查 Receive.BackgroundError 与 Cycle.Error | CycleStopReportsReceiveFaultEvenWhenNoCycleWriteFailed 通过；末次复审无剩余发现 |

## 实际验证与日志

验证根目录：`artifacts/can-validation/`。其中 RED 文件是刻意保留的历史失败证据，不是最终失败。

```powershell
dotnet restore CAN/CanPlugin.Tests/CanPlugin.Tests.csproj --ignore-failed-sources -v minimal
dotnet test CAN/CanPlugin.Tests/CanPlugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/can-validation/tests --logger "trx;LogFileName=can-final.trx" --results-directory artifacts/can-validation/results -v minimal
dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/can-validation/cia-regression --logger "trx;LogFileName=cia-regression-final.trx" --results-directory artifacts/can-validation/results -v minimal
dotnet build CAN/CanPlugin/CanPlugin.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/Release/CAN-1.0.31/Plugins/CAN -v minimal
dotnet build CAN/CanPlugin.UI/CanPlugin.UI.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/Release/CAN-1.0.31/Plugins/CAN -v minimal
git diff --check -- CAN docs/plans
```

- Restore：初次因 NuGet.Config 沙箱访问失败；按审批机制重试成功。该失败未记为通过。
- CAN：32/32，通过；包括3项 STA UI测试、3项生命周期执行器/锁测试，其余为接收/发送/所有权/取消/别名等契约测试。
- CiA402：204/204，通过；最终 CAN 逻辑集成后重跑，不用历史204通过替代当前结果。
- Release 核心/UI：均成功，最终增量构建0警告0错误。此前重编译出现既有 `CanopenExecutorHelper.cs:100 CS8601`，本次未修改该处，不以增量0警告声称仓库不存在此警告。
- 新增 C# 文件：对核心/UI/测试项目执行 `dotnet format whitespace --no-restore --include <新增文件绝对路径> --verify-no-changes`；仅格式化新增文件，不重排无关历史代码。
- CAN本次变更文本：严格 UTF-8 解码检查并拒绝 U+FFFD；保持既有 BOM/EOL 兼容，不将已有中文当其他编码重写。
- 未执行：真实卡/设备、长时间总线压力、系统关机/宿主强制退出、完整平台UI工作流、覆盖率测量。原因：本次使用模拟适配器与组件测试，没有接入/获准操作真实设备；未用构建代替这些证据。

## 风险、兼容与回退

使用与配置详见 `CAN/CONTROLLED_CANOPEN.md`。真实厂商严格发送确认、时钟映射、全部原生驱动队列丢帧计数是明确未完成的适配项目。设备安全停轴/恢复仍归序列与硬件安全链，不能把 CycleStop 当急停。

包目录：`artifacts/Release/CAN-1.0.31/Plugins/CAN`。部署前退出平台、备份完整原CAN目录；宿主SDK契约及CiA402私有依赖加载需集成核对。回退恢复完整备份，并停用新增StepType；旧设置保留，旧版不支持新增服务步骤。没有自动发布、提交或推送。

发布目录核验：158个文件，47.58 MiB，含 ZLG 原生库及部署说明，不含 CAN.ContractTests/xUnit/testhost 依赖。核心和UI程序集版本均为1.0.31.0；36个变更文本（CAN及本次两份计划）严格UTF-8检查通过，无混合换行。核心SHA256为 `2B7DBA3B93D640DB26254EA0B3F58BE7EA09F2AF39055E26B960D25E09F9FA7E`，UI为 `1D9D7192BC21AD3240893481E139C7B55CC4120F1188F383429D6BA4596C8602`。
