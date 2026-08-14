# LIN 插件

## 功能概述

通过 LIN 总线与被测设备通信，支持主节点报文的读、写、写读组合以及调度表周期发送的启停。

## 支持的硬件/协议

- Vector（XL Driver Library）
- PEAK PLIN
- NI-XNET

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| LIN_Close | 关闭 LIN 通道，释放硬件资源。 |
| LIN_Cyclic_SendStart | 启动 LIN 周期发送任务，在后台按各帧配置的周期持续发送多个 LIN 帧，直到执行 LIN_Cyclic_SendStop 停止。 |
| LIN_Cyclic_SendStop | 停止指定名称的 LIN 周期发送任务。 |
| LIN_Open | 打开 LIN 通道并建立连接，支持 LIN 1.x 和 LIN 2.x 协议，可配置为主节点或从节点模式。 |
| LIN_Read | 从 LIN 总线接收一帧数据，可按帧 ID 过滤，结果存入变量。 |
| LIN_Sleep | 使 LIN 总线进入睡眠，支持远程睡眠（发送 Go-to-Sleep 命令）和本地睡眠（仅本地接口）。 |
| LIN_Wakeup | 唤醒 LIN 总线，支持远程唤醒（发送总线唤醒模式）和本地唤醒（仅唤醒本地接口）。 |
| LIN_Write | 向 LIN 总线发送一帧数据（主节点发送帧头和数据）。 |
| LIN_WriteRead | 向 LIN 总线发送帧后等待从机响应，适用于主节点请求-从机应答通信模式。 |

## 使用前提

使用前需安装对应硬件厂商的驱动程序，并先通过 LIN_Open 打开通道。
