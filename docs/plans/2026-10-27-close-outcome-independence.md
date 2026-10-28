# 关闭结果与历史业务故障分离

等级 L3。范围为十插件的关闭/断开/任务释放路径；延续全部插件重新编译、最后仅保留一批完整交付的要求。无设备操作、自动部署或推送。

## 权威契约与边界

- 已通过 GitHub 核对远端 master 开发手册 3.4.0；远端内容 Git blob 与本地同为 `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`。
- §13.3 的 Open/Config 前置仅表达资源和协议依赖，编辑检查为 Warning，不要求此前业务步骤成功；§13.4 的原 caller 取消、内部超时、异常中文 ErrorInfo 规则保留。
- 关闭先冻结本 owner，再等待真实 I/O/回调/受控任务退出并执行合法 SDK 释放。此前业务错误、已退出且不再妨碍最终释放的可选中断失败，只保留诊断，不冒充本次释放失败。
- 真正未退出、SDK 最终释放失败、关闭期限届满或配置错误仍失败；不使用 `catch when IsClosed` 吞掉所有异常，不清空句柄或伪造完成。
- NI-DAQ 非 AO 的 Abort 与 Dispose 分账；AO 指定终值、未知输出、收尾期限和真实 writer 退出约束保留。ACK、步骤完成均不证明设备安全或物理停稳。
- 连接关闭自行收尾其后台任务；单个子任务停止不关闭父连接。精确实例/代次移除和首次重跑围栏保留。

## 能力、架构和兼容取舍

- 主规划使用 ECC planner；架构按 architect 方法检查 owner、SDK 句柄、真实任务和前台观察者的数据流，不引入全局重置或新的设备命令。
- 核心修复以 ECC tdd-workflow 的失败测试驱动；实际 runner 为现有 .NET/xUnit 九项目。当前工作区有大量既有修改，本次不自动混合提交；RED/GREEN 以 TRX、实际命令及最终源码/程序集指纹记录，覆盖率未测不声称达标。
- 主审查使用 csharp-reviewer，超时/取消/诊断专项使用 silent-failure-hunter，最终采用 verification-loop。
- 按项目 L3 规则安排原生子 Agent `close_independence_review` 独立只读审查；不委派实现，不操作设备。最终再提供变更和测试证据复审。
- 不改变 Setting、字段默认值、步骤身份或枚举；更新实际行为 Description、使用文档、版本。无新输入或权限信任边界，安全技能不单独启用；不修改 WPF 编辑交互，保留现有组件回归，完整宿主 E2E 无可安全运行工程，明确未执行。
- 同一构建内直接使用资源本身的诊断状态，不新增独立消费者 API 或额外契约工具。回退须使用完整旧插件及私有依赖，并在实际 I/O 退出后替换，不能热换。

## 阶段与验收

1. **机制复核与失败测试**：逐一核对 CAN、CiA402、Ethernet、HTTP、LIN、Modbus、NI-DAQ、OPC UA、SerialPort、VISA；保留历史失败后 Close 成功、SDK 最终释放失败、真实调用/回调未退出、重试和同名重跑反例。先确认 RED；夹具/编译错误不计产品复现。
2. **最小修复**：优先 LIN 历史周期错误与 NI-DAQ Abort 前置，再修复确认属于同类机制的问题。所有诊断保留来源及异常链，Completed 只按本次真实收尾，Warn 不吞掉真实释放失败。
3. **阶段检查与独立复审**：运行受影响定向测试、核心/UI 构建；核对异常传播、SDK 释放及代次锁序。修复审查问题后重跑受影响用例。
4. **最终集成**：全部十核心/十 UI Release win-x64 重新编译；九套现有软件回归；120 步骤身份、Windows RID 资产、私有 CAN 副本、PE/PDB 与当前源码、UTF-8 和差异检查。
5. **留存**：新包验收后再删除旧包与重复测试二进制，只保留最终 Plugins、完整文件哈希、最终 TRX、构建与验收记录。更新汇总说明，不把软件验证记作硬件或完整宿主验收。

## 已确认的问题

| 模块 | 代码证据 | 处理状态 |
| --- | --- | --- |
| LIN | CloseCore/WaitForWorkers 汇总旧 jobs.Error；物理已释放仍报关闭 Error | 已 RED/定向 GREEN及最终完整回归 |
| NI-DAQ | Release 首行等待已失败的 Abort，导致不尝试最终 Dispose | 已 RED/定向 GREEN；非 AO 在实际 Abort/I/O 退出后调用 Dispose |
| SerialPort / Modbus | Close 实际成功后继续抛出已退出的中断/取消回调错误 | 已 RED/定向 GREEN及最终完整回归 |
| HTTP | Dispose 成功且请求退出后，已退出的取消回调异常仍使关闭任务失败 | 已 RED/定向 GREEN及最终完整回归 |
| OPC UA | 默认 SDK CloseAsync 会隐式 Dispose；重复释放可能用 no-op 掩盖首次失败，且同步取消/诊断可能阻断收尾 | 已核对固定 SDK，显式 CloseAsync(false, ct) 后恰好一次 Dispose；取消/采集诊断与真实退出分账 |
| CAN / LIN 通用周期 | 软超时使工作任务退出早于真实写返回；旧错误阻止移除任务 | 新增每任务真实写票据，9 项 RED→GREEN；未改变 CANopen/适配器/UDS 架构 |
| VISA | 首次 Dispose 设置 disposed 后抛错，第二次 no-op 被误认为释放成功 | 已保留 RED，缓存真实失败，最终完整回归通过 |
| CiA402 / Ethernet | 对照自身控制/资源协议与精确实例关闭 | 本轮无新增行为改动；CiA402 仅升级私有 CAN 依赖 |

NI-DAQ 依据：NI 官方《Learn 10 Functions in NI-DAQmx and Handle 80 Percent of Your Data Acquisition Applications》的 Clear Task，以及 NI-DAQmx .NET Task 文档。仅在实际原生调用与 Abort 已退出后尝试合法 Dispose，不能由文档推导并发销毁句柄。

## 验证记录

本轮回执最终保存在 artifacts/Release/2026-10-27-final-plugins/verification。上一轮包不包含本次修复，不作为本次完成证据；新包验收后才删除旧包。


### 三轮勘误与独立复核

1. **行为复现**：LIN 关闭 2、NI-DAQ 2、串口 1、Modbus 1、HTTP 1、OPC UA 初始 4、CAN 原生写停止 5、LIN 原生写停止 4、VISA 释放 1，共 21 项有真实 RED 回执。初始夹具编译/冷启动超时不计产品复现；测试期限调整后重现真实历史错误。
2. **SDK/并发独立只读审查**：close_independence_review 核对固定 OPC UA SDK 提交 0b23e5f39b570651dc74897080cc6030207d6f7f 的 SessionAsync.cs，默认关闭会隐式 Dispose，已纠正“远端失败必然跳过所有 Dispose”的过度归因。改用 typed bool=false 重载和一次本地 Dispose；Good 不视为服务器删除确认。独立审查还要求保存首次真实 Dispose 失败、停止等待真实 SDK 返回、返回前复查总期限、诊断不能阻碍释放。以上均落实并复审；没有把自审称为独立审查。
3. **最终集成勘误**：另 2 项 OPC 诊断边界 RED→GREEN（抛错 TraceListener、父连接保留子读取故障），增加实际 SDK 形状保护测试。修正历史语义断言、Modbus 说明缩进、自动释放诊断消费者兼容、NI-DAQ 单异常类型兼容；只重跑受影响项目。最后执行全十插件核心/UI构建和九套完整软件回归，见最终验收表。

固定 SDK 依据：https://github.com/OPCFoundation/UA-.NETStandard/blob/0b23e5f39b570651dc74897080cc6030207d6f7f/Libraries/Opc.Ua.Client/Session/SessionAsync.cs 。采用 SDK 官方源码与本机依赖版本，不将远端调用成功、命令 ACK 或真实宿主任务退出解释为物理停稳。

### 未执行与限制

- 没有真实设备动作；完整平台中断/调试退出重入没有安全工程可运行，因此未执行。现有 WPF 组件回归会运行，不能替代完整宿主 E2E。
- 覆盖率未测量；真实驱动挂起/Dispose 返回失败的未知状态仍明确报错，不伪造释放、不越过共享隔离。
- 无权限认证/外部文件协议变化，不新增独立信任边界；无序列设置契约变化，不新增契约工具。
- 编译验证阶段未部署，也未执行提交或推送；最终清单区分当时的源码 HEAD 与未提交修改，不把旧提交号当作唯一构建来源。后续源码提交状态以 Git 为准。

### 最终软件验收

| 测试项目 | 通过/总数 | 失败 | 跳过 |
| --- | --- | --- | --- |
| CanPlugin.Tests | 713/713 | 0 | 0 |
| Cia402Plugin.Tests | 245/245 | 0 | 0 |
| LinPlugin.Tests | 134/134 | 0 | 0 |
| ModbusPlugin.Tests | 177/177 | 0 | 0 |
| NiDaqPlugin.Tests | 120/120 | 0 | 0 |
| OpcUaPlugin.Tests | 202/202 | 0 | 0 |
| PluginRegression.Tests | 448/448 | 0 | 0 |
| SerialPortPlugin.Tests | 88/88 | 0 | 0 |
| VisaPlugin.Tests | 93/93 | 0 | 0 |

全部 9 套 / 2220 项通过，无失败或跳过。采用 Verify-Plugins.ps1 -Mode Test，并在失败定位/修复后对受影响的余下项目使用相同 Release、--no-restore、独立 OutputPath、完整（无 filter）dotnet test 参数续跑。最终 TRX 均对应当前源文件；先前失败和编译错误不冒充通过。

构建/交付命令：Verify-Plugins.ps1 -Mode Package；Verify-PluginDelivery.ps1；Verify-PluginBinarySources.ps1；Verify-PluginSources.ps1；git diff --check。构建、来源及依赖最终结果以同目录回执为准。


最终构建：十核心/十 UI Release win-x64 均 0 警告、0 错误；CAN说明最后澄清后再次完整运行713项并刷新CAN/CiA402成套产物，结果保持通过。Verify-PluginDelivery通过10模块/120步骤/9套2220项/两份私有CAN哈希一致；PE/PDB核对30个产品及测试程序集，共1497次源文档匹配（不是1497个独立文件）。Verify-PluginSources通过1394文本/120独立编辑器；git diff --check通过。

清理前保留tested-binary-source-receipt，清理后再次验证实际交付21个产品程序集。旧包、测试运行二进制和生成目录仅在以上检查成功后删除；TRX/日志/元数据和原始RED回执保留。全程未修改已部署平台或操作设备。
