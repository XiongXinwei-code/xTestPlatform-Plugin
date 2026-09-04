# CAN 插件

## 功能概述

通过 CAN 总线与被测设备通信，支持 CAN 2.0 Classic 与 CAN FD 报文的收发、周期发送，并内置 UDS 诊断（ISO 14229）与 XCP 标定协议步骤。

## 支持的硬件/协议

- Vector（需安装 XL Driver Library）
- PEAK PCAN
- NI-XNET
- ZLG 周立功
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
| UDS_ClearDTC | 清除 ECU 故障码（UDS 服务 0x14）。 |
| UDS_DiagSession | 切换 ECU 诊断会话模式（UDS 服务 0x10）。 |
| UDS_Flash | 通过 UDS 擦除、下载、传输、校验烧录 Intel HEX、S-Record 或 BIN 固件；支持连续映射范围、地址空洞填充、自动块大小与 FlashDriver 激活延时。 |
| UDS_RawRequest | 发送原始 UDS 请求数据（通用，任意服务），适用于其他专用 UDS 插件未覆盖的服务。 |
| UDS_ReadDataByID | 通过 DID 读取 ECU 数据（UDS 服务 0x22），结果以十六进制字符串存入变量。 |
| UDS_ReadDTC | 读取 ECU 故障码（UDS 服务 0x19），结果以十六进制字符串存入变量。 |
| UDS_RoutineControl | 执行 ECU 例程控制（UDS 服务 0x31）。 |
| UDS_SecurityAccess | 执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU，自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。 |
| UDS_WriteDataByID | 通过 DID 向 ECU 写入数据（UDS 服务 0x2E）。 |
| XCP_Connect | 建立 XCP on CAN 连接，发送 CONNECT 命令并获取从站能力信息。 |
| XCP_Disconnect | 断开 XCP on CAN 连接，向从站发送 DISCONNECT 命令。 |
| XCP_ShortDownload | 通过 XCP SHORT_DOWNLOAD 命令向 ECU 内存地址写入最多 6 字节数据（标定参数修改）。 |
| XCP_ShortUpload | 通过 XCP SHORT_UPLOAD 命令从 ECU 内存地址读取最多 7 字节数据。 |

## 使用前提

使用前需安装对应硬件厂商的驱动程序；UDS/XCP 步骤需先通过 CAN_Open 打开通道。

其中 **ZLG 周立功** 的二次开发库（zlgcan.dll、kerneldlls 内核驱动及其依赖的 VC++ 2013 运行库）已随插件一起发布，位于插件目录下的 `Native\Zlg`，无需额外配置 PATH 或安装运行库。

但需注意：**CAN 卡设备本身的 Windows 驱动（.inf/.sys）仍需在测试机安装**。这类内核态驱动必须经系统安装并由设备管理器加载，无法随应用程序目录分发；未安装时设备无法枚举，表现为“打开设备失败”而非“找不到 dll”。

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
