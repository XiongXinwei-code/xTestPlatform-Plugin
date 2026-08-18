# 更新记录

## v1.1.0 - 2026-08-18
- 修复：`ModbusRead` / `ModbusWrite` / `ModbusBatchRead` / `ModbusBatchWrite` 超时不生效的问题。NModbus 的 `Read*Async` / `Write*Async` 不接收 `CancellationToken`，现统一通过 `ModbusHelper.WithTimeoutAsync` 施加软超时
- 新增：`ModbusConnect` 将连接阶段的 `TimeoutMs` 保存到运行时资源，供后续读写步骤复用
- 变更：读写超时后步骤状态返回 `Error`，不再无限阻塞序列
- 修复：执行层与 UI 层 csproj 补充 `RuntimeIdentifier=win-x64`，避免依赖的 RID 专用程序集被占位程序集覆盖

## v1.0.0 - 2026-08-14
- 新增：首次发布
