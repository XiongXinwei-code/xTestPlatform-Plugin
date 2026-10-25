# Ethernet 插件

## 本轮修订 v1.0.23

SOME/IP响应匹配补齐ClientId+SessionId；SD拒绝越界/截断的无符号Entries和Options长度；DoIP车辆公告在发布VIN前验证版本反码和声明长度。不改变InterfaceVersion兼容策略、发现窗口或TCP/UDP通信期限。
、设置键与枚举保持兼容；异常输入更早返回执行错误，未退出的真实I/O继续保留所有权。此说明对应软件验证，硬件和完整平台运行另行验证。

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
| Ethernet_TCP_Close | 关闭并释放指定 ConnectionName 对应的 TCP 连接。 |
| Ethernet_TCP_Open | 建立 TCP 客户端连接并以 ConnectionName 注册，供后续 TcpSend/TcpReceive/TcpClose 步骤使用。 |
| Ethernet_TCP_Receive | 从已建立的 TCP 连接接收数据，结果可存入变量。 |
| Ethernet_TCP_Send | 通过已建立的 TCP 连接发送数据。 |
| Ethernet_UDP_Receive | 绑定本机 UDP 端口并等待接收数据，结果可存入变量。 |
| Ethernet_UDP_Send | 通过 UDP 向目标地址发送数据（无连接，每次新建 Socket）。 |
| SomeIp_FireAndForget | 发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP）。 |
| SomeIp_Request | 发送 SOME/IP RPC 请求并等待响应（支持 UDP/TCP）。 |
| SomeIp_SdDiscover | 通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务 ID/实例 ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）。 |
| SomeIp_Subscribe | 在本地 UDP 端口监听 SOME/IP 事件通知（Notification），按 ServiceId/EventId 过滤。 |

## 使用前提

需保证测试机与被测设备网络可达；DoIP/SOME/IP 步骤需按对应协议配置 IP 与端口。

## 中断、退出与首次重跑

- TCP/DoIP 连接池归属当前 SDK 执行资源表；执行结束或调试上下文回收时先禁止旧代次发布，再在后台释放实际连接，不同步堵塞资源回收线程。
- 下一次打开同名连接会在用户配置的建立期限内等待捕获的旧建立/关闭工作真实退出；旧工作尚未退出或释放失败时明确报错，不能清空占用表后再开。历史错误但已经实际释放的旧实例不再阻挡首次重跑。
- 迟到建立、关闭重试及响应只作用于捕获的实例/代次，不得关闭新连接或覆盖新结果。调用方取消返回 `Aborted`，内部期限/失败返回 `Error`，不会自动重连或重发。
- `UDP Receive.ExpectedLength=0` 接受任意单个数据报长度；正数要求该数据报精确匹配，不拼接、不截断。`BindMode.LocalPort` 保持绑定 Loopback 的既有语义；需要监听外部网卡使用 `AnyInterface`。
- 输出变量需预先声明为 string；前置连接提示仅为 Warning，合法的 Setup/其他块/子序列建立连接不会被当作硬错误。

## 请求与发送期限

- `DoIP_DiagRequest.TimeoutMs` 是该次请求的总通信预算，包含事务排队、发送、ACK、UDS Pending 和完整响应；不再复用 `DoIP_Connect` 的建立期限。排队超时且尚未提交的请求不会关闭正在使用的会话；已开始事务后超时/取消会使捕获的原会话失效，避免旧应答被后续步骤误用。
- `SomeIp_Request.TimeoutMs` 同时控制地址解析、TCP 连接（如适用）、发送和完整响应，阶段切换或收到非匹配报文不会重新计时。UDP 域名解析使用可取消的异步入口。
- `SomeIp_FireAndForget` 和 `Ethernet_UDP_Send` 新增 `SendTimeoutMs`，默认 `3000`，必须为正数，可在各自编辑页面修改。旧 v1 序列缺少该字段时使用默认值；此前发送未设独立上限的序列，应根据实际网络调整此期限。
- 只有原调用方取消返回 `Aborted`，内部期限返回包含中文上下文和异常详情的 `Error`。已开始提交后的超时/取消不意味着报文已撤回，送达或远端执行结果可能未知；插件不会自动重发、自动重连。
- 网络调用本身退出后才释放本次 Socket；`Completed` 对无响应发送仅表示本机调用成功，不能作为远端执行确认。

本机回环、受控异步入口及实际 WPF 控件绑定/卸载测试不代替 ECU、交换机、域名服务或真实负载下的验证。回退时应整体替换同一版本的核心与 UI 目录，不能以混合 DLL 或忽略取消/发送错误恢复旧行为。
