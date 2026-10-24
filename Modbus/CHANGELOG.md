# 更新记录

## v1.0.22 - 2026-10-25
- 6步执行代次覆盖求值/登记、每次实际Master提交与发布，旧续体不用新连接；修复外层读写、SlaveAddress截断、null/枚举、真实DataGrid编辑Flush和变量类型/断连表达式校验。
- 软件验证与三轮勘误见 docs/reviews/2026-10-25-full-plugin-lifecycle-three-rounds.md；真实设备与完整平台调试窗口未验。回退成套替换核心/UI/依赖，不强清真实占用，不把取消/ACK当作动作撤回或物理停止。

## v1.0.21 - 2026-10-24
- 共享实际提交门在 worker 真正入口检查原令牌、单调截止和关闭状态，杜绝未提交的过期操作迟到发送。
- 旧精确 owner 已释放可在首次重连放行，未退出原生/协议操作保持隔离；RTU/TCP 完整事务归属保持。
- 验证：定向 RED/GREEN、跨模块只读复审及最终全量软件验收见 docs/reviews/2026-10-24-plugin-remediation-acceptance.md；真实硬件和完整平台未验证。回退应整目录成对替换 core/UI，保留原包；不得回退为强制清空未释放资源，软件回退不撤销设备输出。

## v1.0.20 - 2026-10-24
- 初始化/关闭拥有独立代次，关闭期限可配置；下一次连接等待已收尾的旧实际资源，不接管活动连接。新增配置同步 UI/校验/兼容测试，刷新卸载前可靠保存。
- 事务锁跟随真实 I/O 退出，超时隔离且取消先于输出；连接执行级持有；修复 Holding UI 映射、非法布尔值、地址/数量及奇数寄存器输入，可靠保存编辑项。
- 核心/UI 同步按补丁版本递增；回退须整目录成对替换，不能回退到未确认 I/O 退出也继续访问的行为。新增软件回归及页面绑定检查；真实驱动、设备时序与动作安全尚未实测。

## 未发布
- 修复：Aborted 仅用于用户取消，内部取消异常保留为 Error，由配套宿主统一记录最终错误。
- 变更：全部步骤正常执行返回 `Completed`，执行有问题返回 `Error`，用户主动取消保留 `Aborted`；依赖原通过/失败状态的序列需相应调整。

## v1.1.0 - 2026-08-18
- 修复：`ModbusRead` / `ModbusWrite` / `ModbusBatchRead` / `ModbusBatchWrite` 超时不生效的问题。NModbus 的 `Read*Async` / `Write*Async` 不接收 `CancellationToken`，现统一通过 `ModbusHelper.WithTimeoutAsync` 施加软超时
- 新增：`ModbusConnect` 将连接阶段的 `TimeoutMs` 保存到运行时资源，供后续读写步骤复用
- 变更：读写超时后步骤状态返回 `Error`，不再无限阻塞序列
- 修复：执行层与 UI 层 csproj 补充 `RuntimeIdentifier=win-x64`，避免依赖的 RID 专用程序集被占位程序集覆盖

## v1.0.0 - 2026-08-14
- 新增：首次发布
