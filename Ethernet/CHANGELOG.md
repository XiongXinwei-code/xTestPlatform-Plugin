# 更新记录

## v1.0.22 - 2026-10-25
- 14步补执行代次/精确连接与结果围栏；TCP/DoIP捕获pool后再查原代次，旧Close不误关新运行。修复UDP精确长度、未知Transport、null回退、DoIP常量校验、输出类型和前置连接Warning。
- 软件验证与三轮勘误见 docs/reviews/2026-10-25-full-plugin-lifecycle-three-rounds.md；真实设备与完整平台调试窗口未验。回退成套替换核心/UI/依赖，不强清真实占用，不把取消/ACK当作动作撤回或物理停止。

## v1.0.21 - 2026-10-24
- DoIP 单次总预算覆盖事务排队、ACK 与诊断响应；TCP/UDP DNS 和发送采用异步、同一原始截止时间。
- UDP 与 SOME/IP 无响应发送新增 SendTimeoutMs，补齐页面、校验和旧序列默认；响应日志后复查原调用取消，禁止迟到发布结果。
- 6 项 TCP/UDP 显示名归入 Ethernet_TCP/Ethernet_UDP；旧 StepTypeId 不变。
- 验证：定向 RED/GREEN、跨模块只读复审及最终全量软件验收见 docs/reviews/2026-10-24-plugin-remediation-acceptance.md；真实硬件和完整平台未验证。回退应整目录成对替换 core/UI，保留原包；不得回退为强制清空未释放资源，软件回退不撤销设备输出。

## v1.0.20 - 2026-10-24
- TCP/DoIP 关闭期限可配置；旧资源实际退出后才重开，关闭记录固定实例/代次，迟到重试不影响新连接。设置解析异常按步骤 Error 返回，编辑器统一防抖与卸载保存。
- DoIP 请求按事务串行化并关联地址/SID，ACK 不重置总期限；TCP/DoIP 连接按执行归属和代次管理，防止迟到登记；修复 SOME/IP 长度溢出和编辑器保存。
- 核心/UI 同步按补丁版本递增；回退须整目录成对替换，不能回退到未确认 I/O 退出也继续访问的行为。新增软件回归及页面绑定检查；真实驱动、设备时序与动作安全尚未实测。

## 未发布
- 修复：`DoIP_DiagRequest.TimeoutMs` 贯穿单次请求排队、ACK/Pending 与完整响应，不复用建立会话期限；排队未提交超时不关闭活动会话，错误保留原异常详情。
- 修复：SOME/IP UDP 域名使用可取消异步解析，解析、连接、发送和响应共用一个单调总预算；不自动重试未知提交。
- 新增：`Ethernet_UdpSend` 与 `SomeIp_FireAndForget` 的 `SendTimeoutMs`（默认 3000 ms），同步设置模型、步骤说明、编辑页面与校验，兼容缺少新字段的旧 v1 序列。
- 验证：新增可控 DNS/连接/写入取消、DoIP 排队/分片、回环正常通信与 WPF 绑定/刷新/卸载回读；真实 ECU 与网络负载尚未实测，回退需完整核心/UI 目录成对替换。
- 修复：仅用户取消返回 Aborted；DoIP 连接超时保留原异常及目标地址，最终错误日志由 SDK 1.2.8 配套宿主统一输出。
- 变更：全部步骤正常执行返回 `Completed`，执行有问题返回 `Error`，用户主动取消保留 `Aborted`；依赖原通过/失败状态的序列需相应调整。

## v1.1.0 - 2026-08-18
- 新增：`Ethernet_TcpSend` 增加 `SendTimeoutMs` 参数（默认 3000 ms），可在编辑器中配置
- 修复：对端不接收数据导致发送缓冲区写满时，`TcpSend` 会永久阻塞序列；现超过 `SendTimeoutMs` 立即终止并返回 `Error`
- 新增：编辑器对 `SendTimeoutMs` 增加校验（`ETH_205`，必须大于 0）

## v1.0.0 - 2026-08-14
- 新增：首次发布
