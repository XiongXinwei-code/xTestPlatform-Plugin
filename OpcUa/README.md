# OpcUa 插件

## 功能概述

通过 OPC UA 协议与 PLC、SCADA 等服务器通信，支持节点的单点与批量读写、订阅监控以及后台数据采集的启停与读取。

## 支持的硬件/协议

- 任何符合 OPC UA 规范的服务器（如西门子、倍福 PLC）

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| OpcUa_BatchRead | 批量读取 OPC UA 服务器中多个节点的值，每个节点的结果分别存入对应变量。 |
| OpcUa_BatchWrite | 批量向 OPC UA 服务器中多个节点写入值。 |
| OpcUa_Connect | 建立 OPC UA 连接，支持匿名和用户名密码认证以及多种安全策略，连接成功后通过 ConnectionName 标识。 |
| OpcUa_DataAcq_Read | 从运行中的 OPC UA 采集任务的 FIFO 缓冲中读取（消费）数据，构造为波形写入变量，可选追加导出 CSV。 |
| OpcUa_DataAcq_Start | 启动 OPC UA 后台数据采集任务，按指定采样间隔定时读取多个节点并写入有界 FIFO 缓冲（仿硬件采集卡模式）， |
| OpcUa_DataAcq_Stop | 停止 OPC UA 后台数据采集任务并释放资源。未被消费的缓冲数据将被丢弃，如需读取请在 Stop 前执行 OpcUa_DataAcq_Read。 |
| OpcUa_Disconnect | 断开指定的 OPC UA 连接并释放资源。 |
| OpcUa_Read | 读取 OPC UA 服务器中单个节点的值，并将结果存入指定变量。 |
| OpcUa_Subscribe | 订阅 OPC UA 节点并等待其值满足指定条件，用于等待 PLC/设备状态变化的场景。 |
| OpcUa_Write | 向 OPC UA 服务器中单个节点写入指定值。 |

## 使用前提

需保证 OPC UA 服务器地址可达，并按服务器要求配置安全策略与凭据。
