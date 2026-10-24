# Modbus / SerialPort / NI DAQ 修复验证记录

任务等级：L3。范围限于三个插件及各自测试；根构建/公共 PendingIoGate/版本由集成方管理，本记录不声明实际设备已通过。

## 已落盘修复

| 审计项 | 修改与边界 |
| --- | --- |
| F01 生命周期与软超时 | 三个插件均由执行上下文拥有资源 wrapper，通过私有源码链接的 PendingIoGate 串行化真实 I/O；调用方取消/期限只停止等待，真实调用未退出前不释放原生句柄、不准入下一事务。显式关闭有界等待、失败仍保留可重试资源，IDisposable 仅请求并观察后台收尾。 |
| 建立期所有权复审 | ModbusLifecycle、SerialPortLifecycle、DaqTaskFactory 在 Connect/Open/配置前 TryAdd 登记本代 owner；Close/Stop 能发现并冻结建立期。迟到成功不得重新发布，确认清理后仅按对象身份移除；失败释放保留登记与错误，供显式关闭重试。默认拒绝重名，不销毁原 owner。 |
| F07 同步 AI/CI | AI 与各路 CI 是独立 NI 任务，CI 使用同设备 AI SampleClock，CI 先 Start、AI 后 Start；每路读取相同 N 个真实样本，不复制单个编码器值。跨设备配置明确拒绝，驱动 Verify 的路由支持与真实同步精度仍需硬件验证。 |
| F08 任务作用域 | 移除全进程静态任务/元数据归属，任务仅在当前执行资源表查找；不依据名字前缀删除其他任务。 |
| F16 串口事务与分帧 | Query 的写入和完整读取属于同一事务；连接拥有余量缓冲，终止符后剩余字节保留给后续读取；旧取消读取真实退出前不能窃取新事务回包。非法读取参数在写入前拒绝。 |
| F17 Holding UI | 下拉选项显式映射 Coil/HoldingRegister，保留序列化枚举值，不再把 Holding 映射成只读 DiscreteInput。 |
| F18 输入 | Coil 仅接受 0/1/true/false；拒绝空项、未知类型和 32 位格式不完整寄存器对。批量写入先完整解析，逐项分别进入 owner gate，任一失败不会迟到继续下一项。 |
| F24 导出能力 | 运行时与 UI 明确不支持 TDMS，不把 CSV 伪装为 TDMS；真实支持 CSV/变量组合，输出条件、枚举与文件大小校验一致。 |
| F25 TaskStop | 成功确认 Abort、真实 I/O 退出与 Dispose 后才移除任务；失败保留 owner 与句柄，可重试，不报告已停止。 |
| R01 编辑保存 | 三个插件 UI 用 Dispatcher 同步保存小型设置，不在 Task.Run 读取可变 ViewModel；批量项、通道集合及项目属性变化立即保存，并在重载时解绑监听。 |
| 文件/时间/位宽 | CSV 路径与任务文件名受限，已有表头必须一致；写入串行、Flush 并可靠轮转，不把出队/读取结束时刻伪装为采样起点，未知 StartTime 使用 DateTime.MinValue。DI/DO 拒绝超过 uint 表达范围与截断，bool 仅单数字线。 |
| DAQ 多动作期限复审 | 配置、每项 Verify、CI→AI Start、AI→CI Read、DI/DO 在每次提交后续 SDK 操作前检查同一单调总期限、调用方 token 与 Close 冻结标志；首项迟到返回不能继续后项。Task.Run 不用取消令牌提前结束，因此仍能确认真实退出。 |
| Modbus pending 发布窗口 | Close 无条件冻结可取消传输，而非根据 HasPendingOperation 瞬时快照；解决已获准 Task.Run 尚未发布 pending 时错过传输取消的窗口。 |

## RED 与修复依据

- 原有 Modbus 32 位寄存器半对输入：三个运行用例原先不抛错，修复后通过；非法 coil/格式也有定向用例。
- UI Holding 回读、批项集合、串口快速重载、DAQ 通道集合原有运行失败，修复后均通过；实际 WPF 页面还在 STA 线程完成构造、Measure/Arrange。
- CSV 越界名字、表头混写、标量假同步导出有五个原有运行失败，修复后通过。
- 真实 Windows TCP loopback：挂起 Read 时旧关闭超时；尝试 Socket.Shutdown 仍未解除该异步读取，未将该尝试记为修复。现用 NModbus IStreamResource 包装 .NET 原生可取消网络 I/O，loopback 关闭用例通过。
- DAQ 在配置期间资源表为空的原有运行失败，修复为预登记 owner。SerialPort/Modbus 建立期冻结与清理失败保留均有对应路径用例。
- DAQ 取消/总期限/Close 后迟到动作的新增契约用例先得到缺少 EnsureOperationActive API 的编译 RED，补齐实现及所有真实调用点后三种状态用例通过；不将编译 RED 称为已现场复现。
- Modbus 传输取消快照用例有真实运行 RED：期望取消次数 1，实际 0（2026-10-24 08:40:00 TRX）；移除瞬时 pending 条件后 GREEN。

## 最终检查命令与结果

以下在工作区根目录执行；三个测试项目均同时构建对应 core 和 UI。使用本机已恢复缓存（无硬件）：

```text
dotnet test Modbus/ModbusPlugin.Tests/ModbusPlugin.Tests.csproj -c Release --no-restore --results-directory Modbus/ModbusPlugin.Tests/TestResults --logger trx -p:OutputPath=D:/project/xTestPlatform_Plugin/Modbus/ModbusPlugin.Tests/bin/Release/ -m:1
dotnet test SerialPort/SerialPortPlugin.Tests/SerialPortPlugin.Tests.csproj -c Release --no-restore --results-directory SerialPort/SerialPortPlugin.Tests/TestResults --logger trx -p:OutputPath=D:/project/xTestPlatform_Plugin/SerialPort/SerialPortPlugin.Tests/bin/Release/ -m:1
dotnet test NiDaq/NiDaqPlugin.Tests/NiDaqPlugin.Tests.csproj -c Release --no-restore --results-directory NiDaq/NiDaqPlugin.Tests/TestResults --logger trx -p:OutputPath=D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin.Tests/bin/Release/ -m:1
git diff --check -- Modbus NiDaq SerialPort
```

| 检查 | 最终结果 | 证据 |
| --- | --- | --- |
| Modbus | 18 通过，0 失败，0 跳过 | Modbus/ModbusPlugin.Tests/TestResults/lei.zeng_RCS26100N_2026-10-24_08_41_07_net8.0.trx |
| SerialPort | 7 通过，0 失败，0 跳过 | SerialPort/SerialPortPlugin.Tests/TestResults/lei.zeng_RCS26100N_2026-10-24_08_41_27_net8.0.trx |
| NI DAQ | 18 通过，0 失败，0 跳过 | NiDaq/NiDaqPlugin.Tests/TestResults/lei.zeng_RCS26100N_2026-10-24_08_41_58_net8.0.trx |
| 差异空白检查 | 退出码 0，无输出 | 上述 git diff --check |

NI DAQ 最终测试前曾因新增测试缺少 System.IO using 编译失败；补齐后重跑 18 项通过，不将该编辑错误作为产品缺陷证据。未测量覆盖率，不宣称覆盖率门槛。

## ECC 与复审

- 使用既有 L3 计划与 ECC TDD、C# 审查、异常传播、安全输入/文件边界及 Windows 桌面验证方法；未安装额外工具、运行 Hooks 或改动设备。
- 主线程的独立只读复审指出三个插件建立期登记缺口，已修正并补用例；后续独立复核与全项目集成由主线程统一记录。
- 多命令风险核对：ModbusBatchWriteExecutor 的 foreach 位于单个 owner.Run 之外，各项单独 await；一个超时会离开循环。DAQ 的复合原生动作需要单调预算检查，已按上表补齐。

## 未验证 / 剩余限制

- 未安装 NI DAQmx 驱动、未连接 DAQ/串口/Modbus 设备；真实 SDK Abort/Verify 支持、AI/CI 公共时钟与触发精度、端口关闭阻塞、串口 RTU 停止和设备安全均未现场验证。
- 本轮软件保证关闭未确认时显式失败并持有资源，不保证不合作的原生驱动一定在某个期限内退出；不得把后台 I/O 超时当成设备已停止输出。
- 未获得硬件采样起点，波形 StartTime 明确未知；若需要绝对 UTC，需要设备支持且另行确认时间关联，不能以读取时间填充。
- TDMS 暂为明确能力错误，不是已实现格式；不支持的跨设备同步配置也明确拒绝。
- WPF 检查是实际控件加载/绑定回读/布局检查，不是完整平台交互 E2E 或硬件运行证明。
- 无 git 提交/推送，无设备动作，未修改 Shared、根构建配置或版本号；构建输出仅写入工作区测试目录。
