# 更新记录

## v1.0.22 - 2026-10-25
- 7步执行代次覆盖打开、实际I/O与迟到波形/标量发布；真实native和取消回调退出前保留owner。修复取消/内部超时区分、null/枚举、文化无关数字、七页String表达式类型及编辑Flush。
- 软件验证与三轮勘误见 docs/reviews/2026-10-25-full-plugin-lifecycle-three-rounds.md；真实设备与完整平台调试窗口未验。回退成套替换核心/UI/依赖，不强清真实占用，不把取消/ACK当作动作撤回或物理停止。

## v1.0.21 - 2026-10-24
- 重新集成共享入口/退役 owner 的修复：保留真正未退出的 I/O 和历史错误，已释放精确 owner 不再导致首次重开失败。
- 本次未新增 VISA 命令；保留批写/WaitOPC 总预算与提交前保护，core/UI 同步重建交付。
- 验证：定向 RED/GREEN、跨模块只读复审及最终全量软件验收见 docs/reviews/2026-10-24-plugin-remediation-acceptance.md；真实硬件和完整平台未验证。回退应整目录成对替换 core/UI，保留原包；不得回退为强制清空未释放资源，软件回退不撤销设备输出。

## v1.0.20 - 2026-10-24
- 关闭及批写总期限可配置，不额外延长用户配置；按实际资源等待旧收尾，迟到 Close 只关闭原实例并保留新连接终止符。编辑器防抖/卸载保存及缺键默认兼容测试齐全。
- 会话持有真实 pending I/O，超时不提前解锁/释放；严格验证 OPC? 返回 1；超时属性失败保证解锁且保留主/清理异常，修复编辑器保存。
- 核心/UI 同步按补丁版本递增；回退须整目录成对替换，不能回退到未确认 I/O 退出也继续访问的行为。新增软件回归及页面绑定检查；真实驱动、设备时序与动作安全尚未实测。

## 未发布
- 变更：全部步骤正常执行返回 `Completed`，执行有问题返回 `Error`，用户主动取消保留 `Aborted`；依赖原通过/失败状态的序列需相应调整。

## v1.1.0 - 2026-08-18
- 修复：`VisaRead` / `VisaQuery` / `VisaWrite` / `VisaBatchWrite` / `VisaWaitOpc` 在驱动层超时失效时会永久阻塞序列。VISA 的 `FormattedIO` 为同步阻塞 API 且不响应 `CancellationToken`，现统一通过 `VisaHelper.RunWithTimeoutAsync` 施加软超时兜底
- 变更：超时后步骤状态返回 `Error`（含超时毫秒数的中文错误信息），仅用户主动中止才返回 `Aborted`
- 新增：`VisaHelper` 增加 `RunWithTimeoutAsync`、`GetIoTimeoutMs` 辅助方法，软超时在会话 `TimeoutMilliseconds` 基础上留出余量

## v1.0.0 - 2026-08-14
- 新增：首次发布
