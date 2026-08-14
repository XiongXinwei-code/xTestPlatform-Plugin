# Modbus 插件

## 功能概述

通过 Modbus 协议与 PLC、仪表等设备通信，支持 TCP/RTU 连接管理及线圈、寄存器的单点与批量读写。

## 支持的硬件/协议

- Modbus TCP 设备（以太网）
- Modbus RTU 设备（串口）

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| Modbus_BatchRead | 批量读取多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式，每个项的读取结果分别存入对应变量。 |
| Modbus_BatchWrite | 批量写入多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式。 |
| Modbus_Connect | 建立 Modbus 连接，支持 TCP 和 RTU（串口）两种传输方式，连接成功后通过 ConnectionName 标识连接。 |
| Modbus_Disconnect | 关闭指定的 Modbus 连接并释放资源。 |
| Modbus_Read | 从 Modbus 设备读取数据，支持多种寄存器类型和数据格式，读取结果存入指定变量。 |
| Modbus_Write | 向 Modbus 设备写入数据，支持线圈和保持寄存器。 |

## 使用前提

需先通过 Modbus_Connect 建立连接；RTU 模式需正确配置串口参数。
