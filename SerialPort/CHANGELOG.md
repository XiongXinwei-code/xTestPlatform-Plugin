# 更新记录

## v1.0.30 - 2026-10-25
- 5步执行代次覆盖求值/登记、逐块串口提交与结果/日志；未退native归属旧owner，释放后首重开正常。修复Read/Query外层发布、null/未知枚举与迟到配置，保持既有读取窗口/Guard语义。
- 软件验证与三轮勘误见 docs/reviews/2026-10-25-full-plugin-lifecycle-three-rounds.md；真实设备与完整平台调试窗口未验。回退成套替换核心/UI/依赖，不强清真实占用，不把取消/ACK当作动作撤回或物理停止。

## v1.0.29 - 2026-10-24
- Read/Query 的 CurrentStep 和设置解析纳入异常边界，损坏数据返回中文 ErrorInfo/完整堆栈，旧 v1 缺字段默认兼容。
- 共享真实提交入口阻止过期或关闭后的未开始 I/O；已释放精确旧 owner 的历史异常不妨碍首次重开，未退出操作仍持有串口。
- 验证：定向 RED/GREEN、跨模块只读复审及最终全量软件验收见 docs/reviews/2026-10-24-plugin-remediation-acceptance.md；真实硬件和完整平台未验证。回退应整目录成对替换 core/UI，保留原包；不得回退为强制清空未释放资源，软件回退不撤销设备输出。

## v1.0.28 - 2026-10-24
- 读取/查询将步骤上下文访问和设置反序列化纳入执行器异常边界；损坏设置或空上下文返回带中文说明及原始堆栈的 Error，不再向宿主逃逸，旧 v1 缺字段默认值保持兼容。
- 打开/关闭总期限可配置；跨执行重开等待原串口实际收尾，超时仍保留句柄/任务追踪；编辑器统一防抖、刷新卸载前 Flush，C# 编码统一 UTF-8 无 BOM。
- 连接持有完整 Write→Read 事务及未退出调用，取消后不允许新查询窃取响应；终止符仅切出首帧并保留余量；打开/关闭和编辑器保存可靠性优化。
- 核心/UI 同步按补丁版本递增；回退须整目录成对替换，不能回退到未确认 I/O 退出也继续访问的行为。新增软件回归及页面绑定检查；真实驱动、设备时序与动作安全尚未实测。

## 未发布
- 修复：Aborted 仅用于用户取消，内部取消异常保留为 Error，由配套宿主统一记录最终错误。
- 变更：全部步骤正常执行返回 `Completed`，执行有问题返回 `Error`，用户主动取消保留 `Aborted`；依赖原通过/失败状态的序列需相应调整。

## v1.1.0 - 2026-08-18
- 修复：`SerialPortWrite` / `SerialPortRead` / `SerialPortQuery` 超时不生效导致序列永久阻塞的问题。`System.IO.Ports` 的 `BaseStream.ReadAsync/WriteAsync` 不响应 `CancellationToken`，现改为同步读写 + 软超时兜底
- 变更：读写超时后步骤状态返回 `Error`（含超时毫秒数的中文错误信息），仅用户主动中止才返回 `Aborted`；不再把"超时未收到数据"当作正常结束
- 修复：执行层与 UI 层 csproj 补充 `RuntimeIdentifier=win-x64`，避免平台无关占位程序集覆盖 Windows 实现，导致运行时报 `System.IO.Ports is currently only supported on Windows`

## v1.0.0 - 2026-08-14
- 新增：首次发布
