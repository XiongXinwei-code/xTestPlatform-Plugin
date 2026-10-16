# 更新记录

## 未发布
- 变更：全部步骤正常执行返回 `Completed`，执行有问题返回 `Error`，用户主动取消保留 `Aborted`；依赖原通过/失败状态的序列需相应调整。

## v1.1.0 - 2026-08-18
- 修复：`VisaRead` / `VisaQuery` / `VisaWrite` / `VisaBatchWrite` / `VisaWaitOpc` 在驱动层超时失效时会永久阻塞序列。VISA 的 `FormattedIO` 为同步阻塞 API 且不响应 `CancellationToken`，现统一通过 `VisaHelper.RunWithTimeoutAsync` 施加软超时兜底
- 变更：超时后步骤状态返回 `Error`（含超时毫秒数的中文错误信息），仅用户主动中止才返回 `Aborted`
- 新增：`VisaHelper` 增加 `RunWithTimeoutAsync`、`GetIoTimeoutMs` 辅助方法，软超时在会话 `TimeoutMilliseconds` 基础上留出余量

## v1.0.0 - 2026-08-14
- 新增：首次发布
