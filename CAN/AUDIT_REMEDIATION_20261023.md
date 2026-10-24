# CAN / CiA402 / LIN 审查修复记录

任务等级 L3；范围为全项目审查 F02–F06、F12、F13、F23、F28、F29 及这些插件的 R01。不修改真实序列、不操作设备、不自动重连，不改变既有 CAN 运动代次、监测 SYNC、NI/ZLG 发送确认、共享 CANopen 隔离或任意 TPDO 路数。

## 修改与验证对应

| 项目 | 修复 | 无设备验证 |
| --- | --- | --- |
| F02 | LIN 连接拥有周期任务与 I/O 门；停止确认 worker 退出；关闭等待全部任务和实际 native 调用，超时保留所有者；后台错误在 Stop/Close 返回 | 阻塞写时 Stop 不提前完成、关闭不先释放句柄、失败任务关闭后登记清理 |
| F03 | CANopen 保留可信采集下界/入口时间；未知时间不刷新为当前时间，CiA402 报告未知计数 | 陈旧入口、未知时间、原有 204 项 CiA402 回归 |
| F04 | 连接级 UDS 事务串行；验证 DID/RID/块序号和已支持响应的必需长度；超时、取消及协议错误隔离 UDS，防迟到响应进入后续事务 | 错 DID、短响应、错 RID、错块序号、并发客户端、超时后重试隔离 |
| F05 | 任意擦除前预检全部段的地址、长度、末地址与 ALFID；编码禁止高位截断 | 编码边界；后段超位宽时实际 Flash 执行器未发送任何帧 |
| F06 | 首次/后续 FC 共用校验；WAIT 有界、Overflow 停止、新 CTS 更新 BS/STmin | 初始 WAIT→CTS、后续 Overflow 不再发 CF |
| F12 | LIN 逐项检查 Stop/Clear，仅已释放句柄清零；清理失败保留重试依据 | Clear 失败后重试；Stop 失败但 Clear 成功仍报错且记录已释放 |
| F13 | Kvaser 部分打开按有效句柄清理，保留关闭失败句柄；外层保留打开与清理双异常，失败资源在登记表中可重试关闭 | 参数失败清理、关闭失败重试、原始异常不被清理覆盖 |
| F23 | 按 NI 官方无 LDF FrameOutStream 能力显式限制配置，UI 禁用不支持选项并说明，不猜测未支持 ABI | LIN1.x、Slave 在打开前拒绝；Classic 数据发送拒绝；真实 WPF View 提示和选项检查 |
| F28 | XCP Connect 输出写入平台变量，不再写资源表 | 实际执行器 fake context 验证 SetVariable 与无 Resources.Set |
| F29 | ISO-TP 重组在分配前检查上限，默认 4 MiB，超限发 Overflow；UdsCommonSetting.MaxResponseBytes 可序列化配置 | 只发送超限首帧即拒绝，无大内存分配 |
| R01 | CAN/LIN 旧编辑器统一使用所属 Dispatcher 的 200 ms 防抖，捕获原步骤，刷新/卸载前 Flush；保留集合项编辑与删除保存 | 即刻刷新/卸载回读；LIN 集合增改删；CAN/LIN 真实 WPF View 布局及绑定回读 |

## 有意义的 RED → GREEN

- 初始 CAN 协议组：12 项原实现失败，修复后通过；随后增加接收长度上限等保护测试。
- Kvaser/UI 初始组：3 项原实现失败，修复后通过。
- Kvaser 外层原始错误保留追加项：原实现实际返回清理 `native -2` 而丢失打开 `native -1`，失败已复现；修复后通过。
- LIN 两项核心原实现失败：Clear 失败不抛错；周期 Stop 在阻塞写未退出时提前完成。修复后均通过。
- 后续集成/兼容/界面测试作为保护测试，不声称均执行过独立 RED。
- CiA402 首轮 192/204：12 项旧 fake 没有入口时间，现显式在假适配器入队时打时间，不在读取时刷新；最终 204/204。

运行命令（输出隔离于仓库内，未覆盖运行平台）：

```text
dotnet test CAN/CanPlugin.Tests/CanPlugin.Tests.csproj --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-remediation/can-tests/ -v minimal --logger trx;LogFileName=can-remediation.trx
dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-remediation/cia-tests/ -v minimal --logger trx;LogFileName=cia-remediation.trx
dotnet test LIN/LinPlugin.Tests/LinPlugin.Tests.csproj --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/plugin-remediation/lin-tests/ -v minimal --logger trx;LogFileName=lin-remediation.trx
```

最终本范围结果：CAN **100/100**（`can-final-100.trx`），CiA402 **204/204**（`cia-remediation.trx`），LIN **11/11**（`lin-final-remediation.trx`）。测试构建同时编译相应 core/UI；主任务另行做 Release 集成和打包。既有 `CanopenExecutorHelper.cs:100` nullable 警告不属于本次引入。

保留失败记录：CAN 一次全量为 97/98，`can-remediation.trx` 中仅旧 NI 预检测试失败。该测试假设 30 ms 内 fake native 已获调度，实际计时先于入口。现测试先后台启动并观察 fake native 入口，再断言调用尚未完成、验证真实预检期限及句柄保留；与已有阻塞 I/O 测试分组隔离，不修改生产超时。同步版本独立重复两次通过（`can-preflight-synchronized*.trx`），随后全量 98/98、补 NRC 0x78 与后续 FC WAIT/新 BS 保护测试后 100/100。未删除失败证据或把失败算作通过。

## 兼容性和现场边界

- UDS 不确定事务结束后不自动重试：执行 CAN_Close 并重新 CAN_Open，再按设备规范恢复会话。此策略比旧版本严格，避免误认迟到同 SID 响应。
- 原始 CAN_Read/XCP 与 UDS 仍不应并行共享同一物理连接；本次事务门只统一多个 UDS 客户端，不宣称跨协议复用安全。
- 未提供可信入口/采集时间的适配器，CiA402 TPDO 新鲜度判据现在返回未知而非伪造新鲜；应先提供适配器时间证据，不应仅增大有效期。
- NI LIN 无 LDF 路径只支持 LIN 2.x 主节点的普通 Enhanced 数据帧；Classic、LIN1.x、Slave 以及 0x3C/0x3D 诊断数据帧明确拒绝。header-only 请求不携带本地响应校验，仍可用。UI 限制与执行器前置检查一致；后续需要 LDF 配置支持，不能把 UI 选中当作 SDK 已应用。
- LIN Stop 确认任务和原生 Write 调用实际退出，不等于证明驱动队列已上总线或物理总线静默。CAN 已有严格 TX 证据机制保持不变。
- 无真实 NI/Kvaser/ZLG/LIN/CANopen/ECU 硬件运行证据；未测现场采样点、队列溢出、USB 断开、ECU 擦除下载或运动安全。不得据本记录宣称现场功能全部通过。

NI 支持范围依据：官方 `ni/grpc-device` 固定提交 `94ce92e0f1e869e2d85aa82906d7111417dc8952` 的 `imports/include/nixnet.h`；[NI 维护的 FrameOutStream 文档](https://nixnet.readthedocs.io/en/stable/api_reference/session.html#nixnet.session.FrameOutStreamSession)。未创建新的未知原生结构或常量。
