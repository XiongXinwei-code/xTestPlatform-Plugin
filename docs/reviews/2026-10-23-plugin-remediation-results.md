# 全插件修复验收记录

任务等级：L3。对应 [原审查报告](2026-10-23-all-plugin-audit.md) 和 [实施计划](../plans/2026-10-23-plugin-audit-remediation.md)。本轮报告的软件修复、测试及本地插件交付已完成；没有操作真实设备、修改实际序列、提交或推送代码。硬件和完整宿主验收不在本次通过结论中。

## 逐项处理

| 项目 | 实施内容与验收方式 |
| --- | --- |
| F01 | VISA/Modbus/DAQ/串口资源持有真实 I/O、事务门和关闭任务；等待取消不释放原生资源；已取消令牌不得提交输出。阻塞 fake 验证门/句柄保持、迟到退出、关闭失败重试。 |
| F02 | LIN 周期任务保存实际 Completion，停止、重启和关闭等待已进入的 Write 退出；后台故障返回 Error。 |
| F03 | CANopen 保留可信采集下界/最早入口，未知时间不作为新鲜；CiA402 fake 仅在入队标记时间。覆盖旧帧、未知时间及独立 TPDO。 |
| F04 | UDS 按适配器串行完整事务，校验短响应、DID/RID、下载块序号和 0x34 响应；失败后隔离，保留 NRC 0x78。 |
| F05 | Flash 在任何擦除前预检所有段起始/结束地址、长度和 ALFID 位宽，非法时零发送。 |
| F06 | 初始/后续 FC 共用状态机，Wait 有限、Overflow 失败，CTS 更新 BS/STmin；取消边界不续发 CF。 |
| F07 | DAQ 独立 AI/CI Task，同设备共享可验证的 AI 采样时钟；按相同 N 点读取，不复制标量；未配置跨设备同步明确拒绝。 |
| F08 | DAQ 任务属于当前执行资源，拒绝重名；初始化 owner 预登记、关闭冻结建立期，失败保持可重试登记。 |
| F09 | OPC 条件仅使用可接受质量的 DataValue；坏质量/null 不能通过 NotEqual 条件。 |
| F10 | DoIP 按会话串行事务，核对 SA/TA/SID，排队、ACK、完整响应共用总期限；取消/协议异常关闭本实例。 |
| F11 | TCP/DoIP 连接执行级所有权，按名字与代次控制建立/关闭，拒绝迟到发布；失败释放保留可重试实例。 |
| F12 | LIN 检查 nxStop/nxClear 错误，只清除已确认释放的句柄；失败可重试。 |
| F13 | Kvaser 部分初始化也释放句柄；CAN_Open 失败预登记所有者，保留打开及清理异常，未释放时可 CAN_Close 重试。 |
| F14 | VISA OPC? 严格解析完成值 1，0/空/其他响应不能 Completed。 |
| F15 | VISA 超时属性 getter/setter、恢复失败均不泄漏事务门；保留主异常及清理异常。 |
| F16 | 串口按首个终止符切帧，连接持有后续余量，覆盖多帧、半帧和跨块终止符。 |
| F17 | Modbus Holding UI 显式映射，不改已有枚举值；实际页面绑定及序列化回读。 |
| F18 | Modbus 严格接受 0/1/false/true，批量完整预检后才输出；地址、数量及奇数寄存器转换也校验。 |
| F19 | OPC 逐条记录质量、源时间、主机入口/序号与后台健康；失败不伪装健康空结果，读取/转换失败不丢 FIFO。 |
| F20 | OPC 订阅从创建前纳入活动与清理；Create/Delete 失败、取消保留主/清理错误，断开等待实际退出。 |
| F21 | OPC 单点/批量请求使用步骤总期限，不并发修改 Session 全局 OperationTimeout；恢复由用户序列实现。 |
| F22 | OPC 采集节点项编辑保存，按 SelectedItem 删除，刷新/卸载 Flush，实际 WPF 页面验证。 |
| F23 | NI 无 LDF FrameOutStream 能力边界明确：不支持的 Slave、LIN1.x、Classic 等在启动前拒绝，UI 同步禁用/说明；未猜测厂商属性。 |
| F24 | DAQ 不再无视导出枚举；支持的 CSV/变量按真实格式处理，未实现 TDMS 明确拒绝而非输出伪 CSV。 |
| F25 | DAQ 停止先冻结并 Abort/等待原任务实际退出，成功后才移除登记；错误保留所有者。 |
| F26 | HTTP JSON 完整语法预检、单次解析；先提取全部映射再更新变量，路径失败不部分写入。宿主 SetVariable 本身无批事务，无法保证宿主写入异常的通用回滚。 |
| F27 | HTTP 临时 handler/client/certificate 完整托管，创建/关闭登记串行，失败不先销毁旧客户端；明确替换取消旧 HTTP 请求，不自动重发。 |
| F28 | XCP 输出用 context.SetVariable，不将变量路径写进资源表。 |
| F29 | ISO-TP 重组分配前限制长度，使用小上限验证超限拒绝，不进行巨额分配实验。 |
| F30 | SOME/IP 无符号边界检查后再转 int；FFFFFFFF/80000000 等返回 null，不抛切片溢出。 |
| R01 | 受影响编辑器在所属 Dispatcher 保存，集合项和刷新/切换可靠回读；不在后台枚举可变集合序列化。 |
| R02 | HTTP 默认认证/客户端证书限定 origin，不自动跨域重定向；URL 日志/结果脱敏，证书忽略有告警；原始负载日志仍是用户控制的诊断能力，敏感内容应关闭该选项。 |

## 直接相关优化

共享 PendingIoGate 以源码私有编译，未新增公共运行时 DLL。错误保留期限来源、底层退出与清理失败；协议隔离不因 UI 返回取消而消失。OPC 波形兼容输出以记录序号为横轴，真实时间分析使用 RecordsJsonVariable；DAQ 时间不声称 DateTime.Now 是采集起点。

修复 CSV 路径、表头、转义、并发与轮转，补逐点结果校验；HTTP ForceConnectionClose 默认 true，允许标准服务显式关闭以复用连接；JSON 多字段共享解析结果。

共九个测试项目进入 slnx（包含原 CAN/CiA402），新增 PR 构建/测试工作流及本地验证脚本；发布流程在测试、构建、严格契约检查和打包成功后才推 tag。未执行实际 GitHub 发布，远端工作流运行仍待提交后的 CI 验证。

## 独立审查与修复闭环

- CAN/CiA402/LIN、IO 三模块、OPC/VISA 分别实施；交叉只读审查由非实施者进行，主线程负责核实、集成及最终验证。
- 第一轮复审发现 Serial/Modbus/DAQ 建立期迟到登记、失败初始化失去可重试所有者；已通过阻塞假驱动 RED 驱动修复并定点复审。
- OPC/VISA 复审发现订阅初始化期 activity 泄漏、VISA 原始句柄配置/清理双失败、区域数值比较、批写超时后续发及登记门期限问题；均修复并纳入最终回归，不以忽略 Error 规避。
- CAN 一次全量出现测试调度 RED（30 ms 期限早于 fake 进入），保留失败 TRX；改为入口同步和隔离测试集后重复与全量通过，不调整生产确认/隔离。

## 验证记录（早期基线与最终集成）

早期集成已执行各模块定向 RED/GREEN 与回归，另执行 20 core/UI 的初次 Release Rebuild（0 错误，仅 CAN 原有 CS8601）。这批旧产物随后已被下述最终源码的重新构建与验证取代，不作为本轮最终交付。

本地工具 `tools/Verify-Plugins.ps1` 的 Test/Package/Validate 模式生成 TRX、构建日志、步骤清单、SHA256 文件清单；输出限定在本仓库 artifacts，不覆盖运行中的平台目录。编译/测试与部署必须使用一致源码状态。

## 2026-10-24：中断重跑、配置与手册范式收口

- 云端 master 手册 blob `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0` 与本地 `git hash-object` 一致。项目 AGENTS 明确该手册为插件开发依据；未更改用户级配置。
- CAN Open 在用户配置总期限内等待/重试确实正在退出的旧 owner，不接管活动通道；Open/Close/Write 的 TimeoutMs 模型、描述、实际编辑页面、上下文校验与旧 v1 缺键默认已成套测试。驱动尚未退出或发送未知仍隔离，不通过清空占用表伪装成功。
- Shared/RetiringResources 私有源码链接各插件，只追踪已收尾对象，不把不同名称的活动 OPC/网络会话变成全局独占；新 Open 使用自己的配置期限等待旧实际任务。Ethernet 关闭记录固定实例/代次，迟到重试不能关闭新连接。
- CAN Write 新增锁前隔离拒绝与最终提交前的原调用方令牌/Stopwatch 期限检查；已进入 native 的发送继续计入真实 I/O。受控取消回调测试确实取得旧版 Sent 包含排队帧的 RED，修复后4/4 GREEN；不声称取消能撤回已发帧。
- OPC Disconnect/VISA Close 捕获当时的原实例，取得登记门后只收尾该实例，删除登记/终止符也要求引用相同。原实例存在/空表两条可控交错各2例真实 RED→GREEN，避免旧关闭请求误杀新代次。
- LIN Close/CyclicStop 主动取消只在原调用方令牌取消时返回 Aborted；内部期限仍 Error。新增4例先出现2个错误状态 RED，修复后27/27 GREEN，并断言 native 未退出时不释放 owner/job。
- CiA402 ShutdownTimeoutMs 是轴内部一次清理总期限，链接活动操作、Cleanup 和释放等待；仅释放自身 CANopen 节点，不关闭父 CAN。独立审查确认此边界。
- 新参数四处闭环及旧 v1 map 缺键默认已验证；所有受影响旧 UI 使用 Dispatcher 防抖、原步骤捕获、刷新/卸载 Flush。实际 WPF 验证走真实编辑器工厂/同一 Dispatcher，未用延时或忽略绑定断言掩盖主题缓存/模板延迟。
- CAN33步骤和 Ethernet/Http20步骤损坏设置纳入实际执行器测试，设置解析移入 try，返回 ErrorInfo/Detail，不在反序列化时越出执行器边界。CAN14个入口与 Ethernet UDP 入口取得真实 RED 后修复。
- 独立只读审查由非实现者按 ECC C# / silent-failure-hunter 方法读取相关调用链、差异和 RED/GREEN TRX；复审关闭 CAN 排队发送、OPC/VISA 旧关闭误关、LIN 取消状态问题。它没有执行设备动作，作者测试与主线程集成测试单独记录。
- `Verify-PluginSources.ps1` 检查所有插件文本 UTF-8、C# 无 BOM、每文件单个步骤编辑器及不得返回 Passed/Failed；串口11个旧 BOM 仅去前导3字节，正文和换行不变。StepInfoExtractor 严格模式额外检查每个步骤匹配唯一 UI 编辑器及非空 Description/IconPath。
- CI 同时安装构建 SDK 与 .NET8；.slnx 需要现代 SDK，目标框架不变。[Microsoft SLNX 支持说明](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/)。未运行远端 CI 或发布。

发布目录发现已验证只识别标准两层 core 项目，排除 tests 及深层 artifacts；发布时步骤清单使用严格模式，不能仅警告后继续发布错误的核心/UI。远端 CI 未实际运行。

## 最终统一验证与本地交付

最终执行（2026-10-24，源码冻结后）：

```text
pwsh -NoLogo -NoProfile -NonInteractive -File tools/Verify-PluginSources.ps1
pwsh -NoLogo -NoProfile -NonInteractive -File tools/Verify-Plugins.ps1 -Mode Test -OutputRoot artifacts/Release/2026-10-24-audit-fixes
pwsh -NoLogo -NoProfile -NonInteractive -File tools/Verify-Plugins.ps1 -Mode Package -OutputRoot artifacts/Release/2026-10-24-audit-fixes
pwsh -NoLogo -NoProfile -NonInteractive -File artifacts/plugin-remediation/final-check.ps1
git diff --check
```

| 测试项目 | 通过 / 总数 |
| --- | --- |
| CAN | 153 / 153 |
| CiA402 | 208 / 208 |
| LIN | 27 / 27 |
| Modbus | 21 / 21 |
| NiDaq | 21 / 21 |
| OpcUa | 39 / 39 |
| SerialPort | 10 / 10 |
| VISA | 29 / 29 |
| Ethernet / Http / Shared | 37 / 37 |
| 合计 | **545 / 545，失败0、跳过0** |

- 20个核心/UI Release Rebuild 全部成功，0错误；仅同一个既存 `CAN/CanPlugin/CANopen/CanopenExecutorHelper.cs:100` CS8601 在 CAN 本体及 CiA402 引用重建时报告，未声称0警告。
- 10个独立目录共118个步骤，严格核心/UI依赖加载、唯一编辑器匹配、版本配对、Windows System.IO.Ports 实现检查通过；部署包无测试程序集。
- 清单中每个文件 SHA256 已重新核对；另复制真实 Http core 到独立负例目录并缺省其 UI，严格检查确实以退出码2拒绝，错误指向缺失匹配编辑器。未删除或修改正常交付目录。
- 最终 UTF-8/C# 无 BOM/编辑器文件/执行状态检查通过，最终差异检查通过。文档更新后再次执行相应静态检查；没有为了文档重复跑未变更的设备逻辑测试。
- 交付根目录：`artifacts/Release/2026-10-24-audit-fixes/`；`Plugins/` 可整目录替换，`INSTALL.md` 说明前提/回退，`build-manifest.json` 记录版本和哈希，`verification/` 保留9份最终TRX、10份构建日志、步骤清单、缺UI负例与 `final-receipt.json`。
- 清单明确 `sourceIncludesUncommittedChanges=true`、`hardwareValidated=false`；这是本地构建，不是已提交/发布版本。此前 `2026-10-23-audit-fixes` 属历史中间产物，勿与本次混用。

能力结果：ECC 规划/架构/TDD/C#与异常传播审查已实际用于相应修复；安全方法用于外部输入/认证/日志边界；WPF 方法形成真实组件页证据；最终验证闭环完成。独立只读审查与实现者自查分开，复审关闭已发现问题；无真实硬件/完整平台验证，未测覆盖率，不把工具未运行说成通过。

### 未执行/边界

- 真实 NI/ZLG、LIN/DAQmx、VISA、串口、PLC/ECU/电机；总线发送顺序、时钟映射、硬件同步路由、停止与驱动退出资格均未实测。
- 完整 xTestPlatform 宿主运行与现场序列未启动；WPF 控件加载/绑定测试和程序集加载不等于现场集成通过。
- 未测量全仓覆盖率；未执行依赖漏洞扫描（本地还原关闭 NuGetAudit），不声称无漏洞或达到覆盖率指标。
- 无法真正中止的 native 调用继续保持引用/隔离并返回未确认关闭错误；不能承诺所有损坏驱动都能在进程内恢复。
- 发布历史已有版本/行为需按原 changelog 识别，回退采用原完整目录，不恢复危险的超时后继续提交策略。
