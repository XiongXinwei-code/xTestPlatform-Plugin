# 更新记录

## v1.1.0 - 2026-08-18
- 新增：`Ethernet_TcpSend` 增加 `SendTimeoutMs` 参数（默认 3000 ms），可在编辑器中配置
- 修复：对端不接收数据导致发送缓冲区写满时，`TcpSend` 会永久阻塞序列；现超过 `SendTimeoutMs` 立即终止并返回 `Error`
- 新增：编辑器对 `SendTimeoutMs` 增加校验（`ETH_205`，必须大于 0）

## v1.0.0 - 2026-08-14
- 新增：首次发布
