# 更新记录

## v1.1.0 - 2026-08-18
- 修复：`SerialPortWrite` / `SerialPortRead` / `SerialPortQuery` 超时不生效导致序列永久阻塞的问题。`System.IO.Ports` 的 `BaseStream.ReadAsync/WriteAsync` 不响应 `CancellationToken`，现改为同步读写 + 软超时兜底
- 变更：读写超时后步骤状态返回 `Error`（含超时毫秒数的中文错误信息），仅用户主动中止才返回 `Aborted`；不再把"超时未收到数据"当作正常结束
- 修复：执行层与 UI 层 csproj 补充 `RuntimeIdentifier=win-x64`，避免平台无关占位程序集覆盖 Windows 实现，导致运行时报 `System.IO.Ports is currently only supported on Windows`

## v1.0.0 - 2026-08-14
- 新增：首次发布
