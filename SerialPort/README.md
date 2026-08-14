# SerialPort 插件

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
