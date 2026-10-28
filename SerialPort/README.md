# SerialPort 插件

当前版本 **1.0.35**：原始 I/O、中断和回调退出且 Dispose 成功后完成关闭；已退出的中断错误保留警告，不污染本次关闭结果。 软件验证与限制见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

修改汇总与交付边界见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

## 功能概述

通过串口（RS-232/485）与设备通信，支持串口的打开关闭、数据读写以及一发一收查询。

## 支持的硬件/协议

- 本机串口或 USB 转串口适配器

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| SerialPort_Close | 关闭指定串口并释放资源。 |
| SerialPort_Open | 打开指定串口并配置通信参数，打开后通过 PortName 标识连接，供后续读写步骤使用。 |
| SerialPort_Query | 向已打开的串口发送数据并读取响应（Write+Read 一体操作），响应存入指定变量。 |
| SerialPort_Read | 从已打开的串口读取数据，结果存入 ResultVariable 指定的变量。 |
| SerialPort_Write | 向已打开的串口写入数据。 |

## 使用前提

需确认串口号、波特率等参数与设备一致。
