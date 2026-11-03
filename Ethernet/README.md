# Ethernet 插件

当前版本 **1.0.32**：TCP/UDP 连接管理独立，新增命名 UDP 连接、广播开关和来源端点输出；TCP 支持终止符分帧，编辑页模式切换与卸载重入同步。

修改汇总与交付边界见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

## 功能概述

通过以太网与被测设备通信，提供 TCP 客户端连接与收发、UDP 收发，并支持车载以太网 DoIP 诊断（ISO 13400）与 SOME/IP 服务调用、订阅及服务发现。

## 支持的硬件/协议

- 标准以太网网卡（TCP/UDP）
- DoIP 诊断网关 / ECU
- SOME/IP 服务节点

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| DoIP_Connection_Connect | 建立 DoIP（ISO 13400）TCP 连接并执行路由激活，以 SessionName 注册会话供后续步骤使用。 |
| DoIP_Diagnostics_DiagRequest | 通过已建立的 DoIP 会话发送 UDS 诊断请求并接收响应。 |
| DoIP_Connection_Disconnect | 关闭并释放指定 SessionName 对应的 DoIP 会话。 |
| DoIP_Discovery_VehicleDiscovery | 通过 UDP 广播发送 DoIP 车辆识别请求，接收车辆公告并解析 VIN 与逻辑地址。 |
| Ethernet_TCP_Connection_Close | 关闭并释放指定 ConnectionName 对应的 TCP 连接。 |
| Ethernet_TCP_Connection_Open | 建立 TCP 客户端连接并以 ConnectionName 注册，供后续 TcpSend/TcpReceive/TcpClose 步骤使用。 |
| Ethernet_TCP_ReadWrite_Receive | 从已建立的 TCP 连接接收数据，结果可存入变量。 |
| Ethernet_TCP_ReadWrite_Send | 通过已建立的 TCP 连接发送数据。 |
| Ethernet_UDP_Connection_Open | 绑定命名 UDP socket（不是远端握手），支持本机地址/端口、广播与实际绑定端点输出。 |
| Ethernet_UDP_Connection_Close | 独立取消并释放命名 UDP socket，不依赖此前收发步骤成功。 |
| Ethernet_UDP_ReadWrite_Receive | 绑定本机 UDP 端口并等待接收数据，结果可存入变量。 |
| Ethernet_UDP_ReadWrite_Send | 通过 UDP 向目标地址发送数据；可选复用命名连接或保持一次性 socket。 |
| SomeIp_ReadWrite_FireAndForget | 发送 SOME/IP 无响应方法调用（RequestNoReturn，支持 UDP/TCP）。 |
| SomeIp_ReadWrite_Request | 发送 SOME/IP RPC 请求并等待响应（支持 UDP/TCP）。 |
| SomeIp_DiscoverySubscription_SdDiscover | 通过 UDP 组播发送 SOME/IP-SD FindService 并收集 OfferService 公告，解析服务 ID/实例 ID/版本及 IPv4 Endpoint 选项（服务实际 IP:端口/协议）。 |
| SomeIp_DiscoverySubscription_Subscribe | 在本地 UDP 端口监听 SOME/IP 事件通知（Notification），按 ServiceId/EventId 过滤。 |

## 使用前提

需保证测试机与被测设备网络可达；DoIP/SOME/IP 步骤需按对应协议配置 IP 与端口。

## 中断、退出与首次重跑

- TCP/DoIP 连接池归属当前 SDK 执行资源表；执行结束或调试上下文回收时先禁止旧代次发布，再在后台释放实际连接，不同步堵塞资源回收线程。
- 下一次打开同名连接会在用户配置的建立期限内等待捕获的旧建立/关闭工作真实退出；旧工作尚未退出或释放失败时明确报错，不能清空占用表后再开。历史错误但已经实际释放的旧实例不再阻挡首次重跑。
- 迟到建立、关闭重试及响应只作用于捕获的实例/代次，不得关闭新连接或覆盖新结果。调用方取消返回 `Aborted`，内部期限/失败返回 `Error`，不会自动重连或重发。
- `UDP Receive.ExpectedLength=0` 接受任意单个数据报长度；正数要求该数据报精确匹配，不拼接、不截断。`BindMode.LocalPort` 保持绑定 Loopback 的既有语义；需要监听外部网卡使用 `AnyInterface`。
- 输出变量需预先声明为 string；前置连接提示仅为 Warning，合法的 Setup/其他块/子序列建立连接不会被当作硬错误。

## TCP 取消后的连接状态

- .NET 8 Windows 回环实测：取消 NetworkStream.ReadAsync 后，TcpClient.Connected 可能变为 false，但原 NetworkStream 仍能实际收发。因此该缓存不能作为已经断线的结论。
- 插件在连接建立时按实际 TcpClient 实例保存 NetworkStream。读取超时仍报 Error，不发布部分结果；后续可继续读取同一响应或发送新数据。若业务不允许沿用不完整响应，应先 Close/Open 清空该连接的接收缓冲。
- 不自动重连、不切换 socket；本地 Close/换代仍由连接池拒绝旧访问，真实对端关闭仍以 EOF 或 I/O 异常报错。新的 Open 使用新的 stream 与缓冲，绝不继承旧尾部。

## 请求与发送期限

- `DoIP_Diagnostics_DiagRequest.TimeoutMs` 是该次请求的总通信预算，包含事务排队、发送、ACK、UDS Pending 和完整响应；不再复用 `DoIP_Connection_Connect` 的建立期限。排队超时且尚未提交的请求不会关闭正在使用的会话；已开始事务后超时/取消会使捕获的原会话失效，避免旧应答被后续步骤误用。
- `SomeIp_ReadWrite_Request.TimeoutMs` 同时控制地址解析、TCP 连接（如适用）、发送和完整响应，阶段切换或收到非匹配报文不会重新计时。UDP 域名解析使用可取消的异步入口。
- `SomeIp_ReadWrite_FireAndForget` 和 `Ethernet_UDP_ReadWrite_Send` 新增 `SendTimeoutMs`，默认 `3000`，必须为正数，可在各自编辑页面修改。旧 v1 序列缺少该字段时使用默认值；此前发送未设独立上限的序列，应根据实际网络调整此期限。
- 只有原调用方取消返回 `Aborted`，内部期限返回包含中文上下文和异常详情的 `Error`。已开始提交后的超时/取消不意味着报文已撤回，送达或远端执行结果可能未知；插件不会自动重发、自动重连。
- 网络调用本身退出后才释放本次 Socket；`Completed` 对无响应发送仅表示本机调用成功，不能作为远端执行确认。

本机回环、受控异步入口及实际 WPF 控件绑定/卸载测试不代替 ECU、交换机、域名服务或真实负载下的验证。回退时应整体替换同一版本的核心与 UI 目录，不能以混合 DLL 或忽略取消/发送错误恢复旧行为。

## 常见 TCP/UDP 测试用法与边界

- TCP 客户端：Open → Send → Receive（任意块、定长或分隔符）→ Close。分隔符默认 CRLF，完整响应长度由 MaxBytes 限制；尾部字节保留在当前 socket 的缓冲中，关闭重开清空。超时保留部分字节并报错，不发布不完整结果。
- UDP 请求/回复：UdpOpen 绑定本地地址/端口 → UdpSend/Receive 启用 UseConnection、使用同名连接 → UdpClose。即使对端在 Receive 步骤执行前回复，数据也可保留在该 socket 的内核缓冲。UDP 不保证可靠送达、顺序或无丢包。
- UDP 临时发送/接收仍是默认行为；命名模式连接缺失报错，不隐式创建或回退。TCP 与 UDP 的同名资源互不影响。
- 广播通过 EnableBroadcast 显式启用；域名异步解析与发送使用同一通信期限，发送成功不代表远端执行。
- 本轮覆盖常用客户端和 UDP 绑定、二进制/文本、分帧、广播、端点、取消与关闭重跑；不宣称覆盖 TCP 服务端、多客户端会话、TLS、通用 UDP 组播或自动重连等专门场景。
- 纯软件 loopback/WPF 回归由本轮交付记录提供；未验证真实网卡广播路由、设备协议或完整平台调试进出。

## 参数写入校验

平台助手与页面使用同一份强类型设置。插件在参数转换及保存前校验：未知或重复字段、类型不符、未定义枚举、不允许的 null、非有限数和赋值后被忽略/改写的值均明确报错，并保留原步骤数据。对象数组需提交完整 JSON 数组；请先读取全部当前项再修改，避免替换时遗漏配置。表达式文本不会在保存时执行，依赖变量或设备的有效性仍由步骤校验与执行处理。
