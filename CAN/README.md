# CAN 插件

## 中断与协议验证边界

- 同名连接只有在旧 owner 已确认真实释放后才允许首次重开；旧收尾历史错误仍输出日志。原生调用未退出、未确认发送或部分 RPDO 周期仍保持隔离，不清空占用强行重开。
- 监测 SYNC/目标启动在确认能力预检和周期门等待中响应步骤取消，并在发布前再次检查同一总期限；启动成功后服务生命周期不绑定短期步骤令牌。
- PEAK/Vector/TOSUN 关闭返回错误时保留尚未释放句柄，可通过关闭步骤重试。Vector/TOSUN 在部分打开后失败也按真实资源状态清理。
- TOSUN 当前为单库所有者模式，仅支持一个并发 TOSUN 连接（不同通道/设备也不能并行）。后续连接在初始化/连接前拒绝，旧 Disconnect 或 Finalize 失败则保留租约到真实释放。官方初始化接口按 FIFO、接收错误帧、使用硬件连接时间三个一字节 bool 传入 `true,false,false`。这不是多通道共享句柄实现，也未验证插件之外的 libTSCAN 消费者共存；部署时应由此插件独占该库生命周期。
- TOSUN Connect 返回 5（已经连接）或失败时返回非零但归属未知的句柄，会保持库隔离，绝不擅自 Disconnect/Finalize。此类冲突不能靠重复打开解决；请关闭同进程其他 libTSCAN 使用者并重启宿主。只有 Connect 成功取得的句柄，其后配置失败才由插件配对断开；普通释放失败仍可重试关闭。
- XCP SHORT_UPLOAD 正应答长度必须完整；UDS 数据不接受残缺半字节。RawRequest 的 0x27 安全访问日志及结果显示隐藏负载，显式结果变量仍保留响应字节供序列使用。
- 原始 CAN 单次/周期发送拒绝残缺半字节、越界标准/扩展 ID、Classic 超过 8 字节或 FD 超过 64 字节；保留空格、连字符、逗号分隔与全部合法 FD DLC。Classic 打开的连接不接受 FD 请求，不依赖厂商截断或隐式降级。通用周期的动态表达式在每次实际发送前校验，后台失败由停止/关闭报告。
- CANopen 反馈的入队时间只作为诊断，不代替可信采集时间。以上均需真实硬件、驱动及设备协议进一步验收。

## 功能概述

1.0.33 修复运动停止与监测 SYNC 并发确认竞态，增加 ZLG USBCANFD-100U/200U 的受控发送回显、连续接收与时间质量支持。NI 统一监测 SYNC、有限点表和所有者停机通路继续保留，反馈数量按列表配置（1~128 路）。使用前请阅读 [配置与能力边界](CONTROLLED_CANOPEN.md)，确认目标型号/驱动支持；软件测试不等于实卡和运动安全验收。

通过 CAN 总线与被测设备通信，支持 CAN 2.0 Classic 与 CAN FD 报文的收发、周期发送，并内置 UDS 诊断（ISO 14229）与 XCP 标定协议步骤。

## 支持的硬件/协议

- Vector（需安装 XL Driver Library）
- PEAK PCAN
- NI-XNET
- ZLG 周立功（USB 及当前 SDK 定义的 PCI / PCIe 型号，含 PCIe CAN FD 系列）
- Kvaser
- TOSUN 同星

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| CAN_Close | 关闭已打开的 CAN 通道并释放硬件资源。 |
| CAN_Cyclic_SendStart | 启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧，直到执行 CAN_Cyclic_SendStop 停止。用于模拟整车网络环境（如车速、转速等信号）。 |
| CAN_Cyclic_SendStop | 停止指定的 CAN 周期发送任务。 |
| CAN_Open | 打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD 协议。 |
| CAN_Read | 从已打开的 CAN 通道接收一帧报文，可按 ID 过滤，结果存入变量。 |
| CAN_Write | 向已打开的 CAN 通道发送一帧报文。 |
| CANopen_Cycle_InputsUpdate | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Cycle_Start | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Cycle_Stop | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Network_EmcyReceive | 等待指定节点上报的 CANopen EMCY 紧急报文，解析错误码、错误寄存器与厂商数据并写入变量；可校验是否为期望的错误码。 |
| CANopen_Network_NmtControl | 发送 CANopen NMT 命令，切换节点的网络管理状态。 |
| CANopen_Network_SyncSend | 发送一帧 CANopen SYNC 同步报文，触发使用同步传输类型（1~240）的 TPDO 刷新或 RPDO 生效。 |
| CANopen_Network_WaitHeartbeat | 等待 CANopen 节点的心跳（或 Boot-up）报文，并确认节点进入期望的 NMT 状态。 |
| CANopen_PDO_Receive | 接收一帧指定 COB-ID 的 CANopen PDO（通常为节点的 TPDO），数据以十六进制字符串存入变量。 |
| CANopen_PDO_Send | 发送一帧 CANopen PDO（通常为节点的 RPDO）。 |
| CANopen_SDO_Read | 通过 SDO 读取 CANopen 节点对象字典中的一个对象（Upload），按数据类型解析后存入变量。支持快速、分段与块传输。 |
| CANopen_SDO_Write | 通过 SDO 向 CANopen 节点对象字典写入一个对象（Download）。支持快速、分段与块传输。 |
| CANopen_Service_SnapshotRead | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Service_Start | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Service_Stop | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| UDS_ClearDTC | 清除 ECU 故障码（UDS 服务 0x14）。 |
| UDS_DiagSession | 切换 ECU 诊断会话模式（UDS 服务 0x10）。 |
| UDS_Flash | 通过 UDS 服务将固件文件烧录到 ECU，依次执行擦除例程（0x31）、请求下载（0x34）、 |
| UDS_RawRequest | 发送原始 UDS 请求数据（通用，任意服务），适用于其他专用 UDS 插件未覆盖的服务。 |
| UDS_ReadDTC | 读取 ECU 故障码（UDS 服务 0x19），结果以十六进制字符串存入变量。 |
| UDS_ReadDataByID | 通过 DID 读取 ECU 数据（UDS 服务 0x22），结果以十六进制字符串存入变量。 |
| UDS_RoutineControl | 执行 ECU 例程控制（UDS 服务 0x31）。 |
| UDS_SecurityAccess | 执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU，自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。 |
| UDS_WriteDataByID | 通过 DID 向 ECU 写入数据（UDS 服务 0x2E）。 |
| XCP_Connect | 建立 XCP on CAN 连接，发送 CONNECT 命令并获取从站能力信息。 |
| XCP_Disconnect | 断开 XCP on CAN 连接，向从站发送 DISCONNECT 命令。 |
| XCP_ShortDownload | 通过 XCP SHORT_DOWNLOAD 命令向 ECU 内存地址写入最多 6 字节数据（标定参数修改）。 |
| XCP_ShortUpload | 通过 XCP SHORT_UPLOAD 命令从 ECU 内存地址读取最多 7 字节数据。 |

## 使用前提

UDS/XCP/CANopen 步骤需先通过 CAN_Open 打开通道（CANopen 使用标准 11 位帧，建议 CAN 2.0 Classic）。

CANopen SDO 步骤可在编辑器中导入设备 EDS/DCF 文件，从对象列表中选择后自动填入 Index、SubIndex 与数据类型；EDS 仅用于编辑时选择，运行时不依赖该文件。

其中 **ZLG 周立功** 的通用二次开发库（zlgcan.dll、现有 USB 板卡库及 VC++ 2013 运行库）随插件发布，位于插件目录下的 `Native\Zlg`，无需额外配置 PATH。
**当前仓库尚未包含 PCI / PCIe 专用板卡 DLL**，使用这些卡前需从官方 x64 二次开发包补齐所需库及依赖，保持 `kerneldlls` 目录结构后重新构建；设备 XML 不代表依赖完整。

但需注意：**CAN 卡设备本身的 Windows 驱动（.inf/.sys）仍需在测试机安装**。这类内核态驱动必须经系统安装并由设备管理器加载，无法随应用程序目录分发；未安装时设备无法枚举，表现为“打开设备失败”而非“找不到 dll”。

### ZLG PCI / PCIe 配置

`AdapterType` 选择 `ZLG`，`Channel` 表达式填写完整通道名，如 `"PCI5010U/0/0"`、
`"PCIe9120/0/1"`、`"PCIE-CANFD-200U/0/1"`。索引从 0 开始，型号不区分大小写，
也接受 SDK 的 `ZCAN_*` 名称。

- 支持 SDK 中全部 PCI / PCIe 型号码，包括 PCIe CAN FD 的 100U / 200U / 400U、
  EX / MINI / M2、800U / 1200U 及 MINI-PCIE-CANFD；EX 与 M2 按 SDK 共用设备码。
- PCI5010U / PCI5020U 使用驱动标准波特率，**采样点由驱动决定，不应用目标采样点**，日志会明确说明。
- PCIe CAN FD 型号在 Classic / FD 下均使用 80% 仲裁段采样点；FD 的 5 Mbps 数据段使用 75%，
  其他 SDK 标准数据段使用 80%。超出型号标准波特率范围时明确报错。
- 板卡库缺失时，打开失败会提示所需 DLL；PCIe9220 在当前 SDK 的 `dll_cfg.ini` 中没有映射，需使用包含该型号的官方 SDK。
- 完整型号、波特率和 DLL 对照表见 [ZLG 原生库说明](CanPlugin/Native/Zlg/x64/README.md)。本次未进行 PCI / PCIe 实卡收发验证。

## UDS_Flash 映射范围

部分 ECU 的 FlashDriver 要求按完整的 APP 映射区间擦除、下载和计算 CRC；即使 Intel HEX 中有地址空洞，也必须发送连续范围。此时在 `UDS_Flash` 的“映射与填充”页启用“使用映射范围”，填写映射起始地址、映射结束地址（含）和填充字节。

例如 ZLG 文件下载配置为 `0x02000000` 至 `0x0237FFFF`、填充 `0x00` 时，对应配置为：

- 映射起始地址：`"0x02000000"`
- 映射结束地址：`"0x0237FFFF"`
- 空洞填充字节：`"0x00"`
- 地址长度格式：`"0x44"`
- 数据格式标识：`"0x00"`
- 擦除携带地址和长度：勾选
- 单块最大字节：`0`（完全采用 ECU 的 `$34` 响应）

若 APP 下载需要等待 FlashDriver 激活，可将“下载前延时”设为 ECU 规范要求的值，例如 `500 ms`。启用“输出日志”后，步骤会打印擦除和 `$34` 请求的 TX/RX 数据，便于与 ZLG 成功抓包逐字节比对。
