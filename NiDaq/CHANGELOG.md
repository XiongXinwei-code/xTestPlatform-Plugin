# 更新记录

## v1.1.0 - 2026-08-18
- 修复：`NiDaqAiRead` / `NiDaqSyncRead` / `NiDaqEncoderRead` / `NiDaqDiRead` / `NiDaqDoWrite` 会永久阻塞序列的问题。DAQmx 的 Reader/Writer 为同步阻塞 API，且未配置超时时 `Stream.Timeout` 被设为 -1（无限等待）
- 新增：`NiDaqTimeoutHelper` 软超时辅助类，未配置超时的步骤使用 10 秒默认软超时
- 变更：超时后步骤状态返回 `Error`，不再无限等待设备响应

## v1.0.0 - 2026-08-14
- 新增：首次发布
