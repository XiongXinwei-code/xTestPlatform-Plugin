# 更新记录

## v1.1.0 - 2026-08-18
- 新增：`LabVIEWCall` 增加 `TimeoutMs` 参数（默认 0 表示不限制），可在编辑器中配置
- 修复：VI 调用为阻塞式非托管调用、无法响应取消，VI 不返回时会永久阻塞序列；现配置 `TimeoutMs` 后超时终止等待并返回 `Error`
- 新增：编辑器对 `TimeoutMs` 增加校验（`LV_TIMEOUT_INVALID` 负数报错、`LV_TIMEOUT_UNLIMITED` 填 0 时警告可能永久阻塞）

## v1.0.0 - 2026-08-14
- 新增：首次发布
