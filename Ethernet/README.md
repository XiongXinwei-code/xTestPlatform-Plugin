# Ethernet 插件

## 功能概述

通过以太网与被测设备通信，提供 TCP 客户端连接与收发、UDP 收发，并支持车载以太网 DoIP 诊断（ISO 13400）与 SOME/IP 服务调用、订阅及服务发现。

## 支持的硬件/协议

- 标准以太网网卡（TCP/UDP）
- DoIP 诊断网关 / ECU
- SOME/IP 服务节点

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| DoIP_Connect | 建立 DoIP（ISO 13400）TCP 连接并执行路由激活，以 SessionName 注册会话供后续步骤使用。 |
| DoIP_DiagRequest | 通过已建立的 DoIP 会话发送 UDS 诊断请求并接收响应。 |
| DoIP_Disconnect | 关闭并释放指定 SessionName 对应的 DoIP 会话。 |
| DoIP_VehicleDiscovery | 通过 UDP 广播发送 DoIP 车辆识别请求，接收车辆公告并解析 VIN 与逻辑地址。 |
| Ethernet_TcpClose | 关闭并释放指定 ConnectionName 对应的 TCP 连接。 |
| Ethernet_TcpOpen | 建立 TCP 客户端连接并以 ConnectionName 注册，供后续 TcpSend/TcpReceive/TcpClose 步骤使用。 |
| Ethernet_TcpReceive | 从已建立的 TCP 连接接收数据，结果可存入变量。 |
| Ethernet_TcpSend | 通过已建立的 TCP 连接发送数据。 |
| Ethernet_UdpReceive | 绑定本机 UDP 端口并等待接收数据，结果可存入变量。 |
| Ethernet_UdpSend | 通过 UDP 向目标地址发送数据（无连接，每次新建 Socket）。 |
| SomeIp_FireAndForget | 发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP）。 |
| SomeIp_Request | 发送 SOME/IP RPC 请求并等待响应（支持 UDP/TCP）。 |
| SomeIp_SdDiscover | 通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务 ID/实例 ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）。 |
| SomeIp_Subscribe | 在本地 UDP 端口监听 SOME/IP 事件通知（Notification），按 ServiceId/EventId 过滤。 |

## 使用前提

需保证测试机与被测设备网络可达；DoIP/SOME/IP 步骤需按对应协议配置 IP 与端口。
