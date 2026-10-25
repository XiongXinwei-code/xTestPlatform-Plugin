# NiDaq 插件

## 本轮修订 v1.0.24

六个前台步骤在任务查询前后核对原始取消和执行代次；保留AO配置、单点输出与任务实际停止和释放能力。

设置字段、枚举与步骤身份保持兼容；非空输出须按手册显式声明完整变量路径。软件验证与三轮勘误记录在源码仓库 docs/reviews/2026-10-25-plugin-lifecycle-followup.md。真实平台调试窗口和设备尚需验收。

## 功能概述

基于 NI-DAQmx 进行数据采集与信号输出，支持模拟量采集、AO 电压单点输出、数字量读写、编码器计数以及多任务同步采集。

## 支持的硬件/协议

- NI 数据采集卡（USB / PCIe / cDAQ 等，需 NI-DAQmx 驱动）

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| NiDaq_AI_Config | 配置 NI DAQ AI 模拟输入采集任务（通道、终端、电压范围、时钟、触发），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_AI_Read | 从已启动的 AI 采集任务中读取数据，可导出为文件并/或将结果存入变量。 |
| NiDaq_AO_Config | 配置同一 NI 设备/模块的按需 AO 电压单点输出任务。此步骤验证通道和配置，但不调用电压写入；配置/预留阶段的电气行为由设备定义。 |
| NiDaq_AO_WriteSingle | 向已配置且成功 TaskStart 的 AO 电压任务写入一组单点值；不创建新任务、不自动启动、不改变通道顺序。 |
| NiDaq_DI_Read | 读取 NI DAQ 数字输入通道的状态值，存入变量。 |
| NiDaq_DO_Write | 设置 NI DAQ 数字输出通道的状态值。 |
| NiDaq_Encoder_Config | 配置 NI DAQ 编码器采集任务（Counter 通道、解码类型、脉冲数、单位），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_Encoder_Read | 从已配置的编码器任务中读取当前位置值，存入指定变量。 |
| NiDaq_Sync_Config | 配置 NI DAQ 同步采集任务（AI 通道 + 编码器通道、共享时钟/触发），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_Sync_Read | 从已启动的同步采集任务中读取 AI 和编码器对齐数据，可导出为文件并/或将结果存入变量。 |
| NiDaq_Task_Start | 启动已配置的 NI DAQ 任务（AI/编码器/同步采集，或 AO 单点输出）。 |
| NiDaq_Task_Stop | 停止并释放 NI DAQ 任务（AI/编码器/同步采集或 AO 单点输出，亦可清理配置/启动失败的任务）。 |

## 使用前提

需安装 NI-DAQmx 驱动，并在 NI MAX 中确认设备名称。

## AO 电压单点输出

顺序：`NiDaq_AO_Config` → `NiDaq_Task_Start` → `NiDaq_AO_WriteSingle`（可多次）→ `NiDaq_Task_Stop`。仅支持按需电压输出，不提供硬件波形、周期或多通道电气同步保证。

- 配置任务名使用字符串表达式，如 `"AO1"`；每行一个同设备/模块的物理通道，如 `Dev1/ao0`、`Dev1/ao1`，量程按设备手册填写。初值表达式如 `new double[] { 1.0, 2.0 }`，按通道行序对应电压 V；单通道也可直接填 `1.0`。
- 配置只创建/验证，**TaskStart 才显式启动并写初值**；配置/Start 期间设备本身的电气行为仍须查手册，不保证无瞬态。WriteSingle 不会隐式启动，数量、NaN/无穷、越界均拒绝，不补零、不截断。
- 停止策略必须明确选择：`DeviceDefined` 接受设备 Stop/释放行为，不承诺末值保持、高阻或安全电压；`WriteFinalValues` 还必须填写有序终值表达式。默认 `Unspecified` 不允许运行，不能假设 0 V 安全。
- `CleanupTimeoutMs` 限制真实 AO 收尾，包括等待原写退出、指定终值与停止。TaskStop 的 `TimeoutMs` 是本次调用等待预算；超时/取消并不撤销已开始写入，不允许新运行抢占旧 writer。
- 半启动失败仍持有真实资源并按策略收尾。终值成功后重试释放不重复写；准备阶段尚未提交的失败可显式重试，终值已经提交但结果未知会保留隔离，不自动重发。请核实设备输出与日志，不能通过清空资源表来“恢复”。
- Completed 仅说明 SDK 调用返回，不是实测电压、设备安全或电机停稳。实际控制外设前须独立验证输出极性、量程、接线及停止处置。

## 中断、导出与验证边界

AI/同步读取的 `ReadTimeoutMs` 与新增 `ExportTimeoutMs` 分开配置；真实导出在途时保留任务与文件锁。配置、读写和回收不再同步预枚举全部 NI 设备。正常中断后等待原任务真实释放，再配置同物理资源；驱动永久不退出时只能明确隔离，不能承诺强制立即重跑。

本机已只读确认 NI-DAQmx/NI-XNET 26.5 安装，但本次未创建真实 DAQ 设备任务、未输出电压。组件 WPF 与替身测试不代替完整平台及硬件验收；详见项目 `docs/reviews/2026-10-24-plugin-remediation-acceptance.md`。

DI/DO 的实际表达式编辑框接收完整 `SequenceFile`/`EditPosition` 上下文；AI/同步/编码器配置在编辑校验时拒绝非有限数值、非法枚举和启用却未填写的触发源，仍由执行器/DAQmx 校验实际设备能力。参数 Description 的默认值与模型同步，编码器读取默认期限为 10000 ms，`-1` 保持原有可取消无限读取语义。

本轮另以真实平台 SDK 注册表验证 `CleanupExecution`（原表重用）与 `CleanupAll`（新调试表）后，前一 fake 物理任务实际释放前不得开始下一次配置；这不是完整调试窗口端到端或硬件时序证据。当前审查及 62 项软件回归证据见 `artifacts/full-plugin-audit-20261024/initial-nidaq-opc.md`。

第一轮独立勘误后，同一资源表中因中断进入 closing/closed 的旧任务可在本次配置期限内等待并按精确实例移除，首次重配不再被旧登记误挡；活动任务和 AO 输出结果未知仍拒绝接管。结果写入和 Completed 前重新核对原 caller 与当前 owner。AI/同步通道表按选中行删除，卸载/刷新先提交正在编辑的单元格；同步编码器可配置原有 `ZIndexEnable`。

旧 AI/编码器/同步配置、TaskStart/Stop、DI/DO 的 `TimeoutMs` 计时范围是名称/通道/值表达式求值后的 SDK、排队及相关收尾阶段，并非可抢占任意表达式的全步骤期限；求值完成后会重查原 caller，取消后不继续登记/设备动作。AO 的既有更严格表达式+I/O 总预算保持不变。表达式引擎内部永久停顿的终止能力属于宿主边界，本轮未证明可强制中止。

所有 12 步在首次等待前捕获 SDK 执行代次。资源表回收（即使 caller token 尚未取消）后，旧步骤不能查找新运行的同名任务、迟到配置或提交 DI/DO；普通 I/O 与发布返回中文 Error。宿主显式中断标志或原 caller 取消仍为 Aborted。代次检查不阻止已经启动的专用 Stop/Dispose 收尾：AO 仍等待真实 writer 退出，再按明确策略处理终值，未知终值不自动重发。第二轮软件证据见 `artifacts/full-plugin-audit-20261024/round2-nidaq-opc-epoch.md`。
