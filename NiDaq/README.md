# NiDaq 插件

当前版本 **1.0.32**：扩展持久数字量、通用计数器输入/输出和独立编码器端子配置；保留既有任务归属与确认释放机制。软件验证与限制见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

修改汇总与交付边界见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

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
| NiDaq_Digital_DIConfig | 配置持久 DI 端口/线组，支持按需或设备支持的采样时钟、启动触发。 |
| NiDaq_Digital_DOConfig | 配置持久 DO 端口/线组，缓冲模式先写数据再 Start。 |
| NiDaq_Digital_Read | 读取已启动 DI 的多通道 UInt32 数据，按通道、样本写入完整 JSON。 |
| NiDaq_Digital_Write | 按物理端口位写入单点或多通道缓冲 UInt32 数据；不隐式启动。 |
| NiDaq_Counter_InputConfig | 创建边沿计数或单计数器测频任务，可配置边沿、计数方向/初值和输入端子。 |
| NiDaq_Counter_Read | 从已启动普通 CI 读取 UInt32 边沿计数或 double Hz；不可混读 Encoder。 |
| NiDaq_Counter_OutputConfig | 配置频率、占空比、初始延迟、输出端子与有限/连续脉冲；Start 才启动。 |
| NiDaq_Encoder_Config | 配置 NI DAQ 编码器采集任务（Counter 通道、解码类型、脉冲数、单位），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_Encoder_Read | 从已配置的编码器任务中读取当前位置值，存入指定变量。 |
| NiDaq_Sync_Config | 配置 NI DAQ 同步采集任务（AI 通道 + 编码器通道、共享时钟/触发），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_Sync_Read | 从已启动的同步采集任务中读取 AI 和编码器对齐数据，可导出为文件并/或将结果存入变量。 |
| NiDaq_TaskManagement_Start | 启动已配置的 AI/AO/DI/DO/CI/CO/编码器或同步任务。 |
| NiDaq_TaskManagement_Stop | 停止并释放指定任务；不要求此前读写成功，亦可清理配置/启动失败的任务。 |

## 使用前提

需安装 NI-DAQmx 驱动，并在 NI MAX 中确认设备名称。

## 数字量、计数器和编码器扩展

- 持久数字输入：`Digital_DIConfig → TaskManagement_Start → Digital_Read → TaskManagement_Stop`。数字输出按需模式为 `Digital_DOConfig → Start → Digital_Write → Stop`；硬件缓冲输出先 `DOConfig → Digital_Write` 预装，再 `Start → Stop`。原有 DI_Read / DO_Write 瞬时步骤保留。
- Channels 为字符串表达式，如 `"Dev1/port0,Dev1/port1/line4:7"`，每个逗号条目对应数据一行。数组顺序为 `[通道, 样本]`；单通道接受整数或一维数组，多通道可填 `new uint[,] {{1,2},{0x10,0x20}}`，也可使用已物化 List。延迟枚举需先 `ToArray()`；不接收字符串数值、小数或自定义转换对象。
- UInt32 保留物理端口位位置：`line4:7` 全高值是 `0xF0`，`line7` 是 `0x80`，不压缩到低位；写入未选中的位明确报错。每组最多 32 位，支持 line0~line31；不静默截断。
- Digital_Read 输出 string JSON，示例：`{"channels":["Dev1/port0","Dev1/port1"],"samplesPerChannel":2,"data":[[1,2],[3,4]]}`。实际样本不完整时返回 Error，不伪造样本。采样时钟、启动触发、缓冲数字量及端子路由是否可用由设备和 DAQmx 验证，不退化为软件轮询。
- 普通计数器：`Counter_InputConfig → Start → Counter_Read → Stop`。边沿计数返回 UInt32；频率模式采用 LowFrequencyOneCounter 并返回 Hz double。脉冲输出：`Counter_OutputConfig → Start → Stop`；配置只建立/验证，有限脉冲数完成后仍应 Stop 释放资源。停止后的电气状态由设备决定，不承诺高阻或外部装置安全。
- 独立 Encoder_Config 支持 A/B/Z 显式输入终端、原始 Ticks 初值、Z 复位值/相位；留空端子和零位置兼容旧设置。Encoder_Read 与普通 Counter_Read 互斥。Sync_Config 保持同设备 AI + Encoder 共享时钟/触发及旧默认路由，不宣称通用跨设备同步。
- 本轮依据本机 `NIDAQmx.h` 与 NI-DAQmx .NET 26.5.40.49613 核对接口，参考 [NI 通道创建契约](https://github.com/ni/grpc-device/wiki/NI-DAQMX-Channel-Configuration-And-Creation)、[NI 数字数据位规则](https://www.ni.com/docs/en-US/bundle/ni-daqmx/page/digitaldata.html) 和 [NI 时序/同步说明](https://www.ni.com/en/support/documentation/supplemental/06/timing-and-synchronization-features-of-ni-daqmx.html)。仍未覆盖全部传感器类型、双计数器测频、通用跨设备路由等 DAQmx 能力。

## AO 电压单点输出

顺序：`NiDaq_AO_Config` → `NiDaq_TaskManagement_Start` → `NiDaq_AO_WriteSingle`（可多次）→ `NiDaq_TaskManagement_Stop`。仅支持按需电压输出，不提供硬件波形、周期或多通道电气同步保证。

- 配置任务名使用字符串表达式，如 `"AO1"`；每行一个同设备/模块的物理通道，如 `Dev1/ao0`、`Dev1/ao1`，量程按设备手册填写。初值表达式如 `new double[] { 1.0, 2.0 }`，按通道行序对应电压 V；单通道也可直接填 `1.0`。
- 配置只创建/验证，**TaskStart 才显式启动并写初值**；配置/Start 期间设备本身的电气行为仍须查手册，不保证无瞬态。WriteSingle 不会隐式启动，数量、NaN/无穷、越界均拒绝，不补零、不截断。
- 停止策略必须明确选择：`DeviceDefined` 接受设备 Stop/释放行为，不承诺末值保持、高阻或安全电压；`WriteFinalValues` 还必须填写有序终值表达式。默认 `Unspecified` 不允许运行，不能假设 0 V 安全。
- `CleanupTimeoutMs` 限制真实 AO 收尾，包括等待原写退出、指定终值与停止。TaskStop 的 `TimeoutMs` 是本次调用等待预算；超时/取消并不撤销已开始写入，不允许新运行抢占旧 writer。
- 半启动失败仍持有真实资源并按策略收尾。终值成功后重试释放不重复写；准备阶段尚未提交的失败可显式重试，终值已经提交但结果未知会保留隔离，不自动重发。请核实设备输出与日志，不能通过清空资源表来“恢复”。
- Completed 仅说明 SDK 调用返回，不是实测电压、设备安全或电机停稳。实际控制外设前须独立验证输出极性、量程、接线及停止处置。

## 中断、导出与验证边界

AI/同步读取的 `ReadTimeoutMs` 与新增 `ExportTimeoutMs` 分开配置；真实导出在途时保留任务与文件锁。配置、读写和回收不再同步预枚举全部 NI 设备。正常中断后等待原任务真实释放，再配置同物理资源；驱动永久不退出时只能明确隔离，不能承诺强制立即重跑。

本机已只读确认 NI-DAQmx/NI-XNET 26.5 安装，但本次未创建真实 DAQ 设备任务、未输出电压。组件 WPF 与替身测试不代替完整平台及硬件验收；详见项目 `../docs/PLUGIN_CHANGES.md`。

DI/DO 的实际表达式编辑框接收完整 `SequenceFile`/`EditPosition` 上下文；AI/同步/编码器配置在编辑校验时拒绝非有限数值、非法枚举和启用却未填写的触发源，仍由执行器/DAQmx 校验实际设备能力。参数 Description 的默认值与模型同步，编码器读取默认期限为 10000 ms，`-1` 保持原有可取消无限读取语义。

本轮另以真实平台 SDK 注册表验证 `CleanupExecution`（原表重用）与 `CleanupAll`（新调试表）后，前一 fake 物理任务实际释放前不得开始下一次配置；这不是完整调试窗口端到端或硬件时序证据。历史过程日志已清理，当前修改范围见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

第一轮独立勘误后，同一资源表中因中断进入 closing/closed 的旧任务可在本次配置期限内等待并按精确实例移除，首次重配不再被旧登记误挡；活动任务和 AO 输出结果未知仍拒绝接管。结果写入和 Completed 前重新核对原 caller 与当前 owner。AI/同步通道表按选中行删除，卸载/刷新先提交正在编辑的单元格；同步编码器可配置原有 `ZIndexEnable`。

旧 AI/编码器/同步配置、TaskStart/Stop、DI/DO 的 `TimeoutMs` 计时范围是名称/通道/值表达式求值后的 SDK、排队及相关收尾阶段，并非可抢占任意表达式的全步骤期限；求值完成后会重查原 caller，取消后不继续登记/设备动作。AO 的既有更严格表达式+I/O 总预算保持不变。表达式引擎内部永久停顿的终止能力属于宿主边界，本轮未证明可强制中止。

所有 19 步在首次等待前捕获 SDK 执行代次。资源表回收（即使 caller token 尚未取消）后，旧步骤不能查找新运行的同名任务、迟到配置或提交 DI/DO；普通 I/O 与发布返回中文 Error。宿主显式中断标志或原 caller 取消仍为 Aborted。代次检查不阻止已经启动的专用 Stop/Dispose 收尾：AO 仍等待真实 writer 退出，再按明确策略处理终值，未知终值不自动重发。验证边界见 [插件修改说明](../docs/PLUGIN_CHANGES.md)。

## 参数写入校验

平台助手与页面使用同一份强类型设置。插件在参数转换及保存前校验：未知或重复字段、类型不符、未定义枚举、不允许的 null、非有限数和赋值后被忽略/改写的值均明确报错，并保留原步骤数据。对象数组需提交完整 JSON 数组；请先读取全部当前项再修改，避免替换时遗漏配置。表达式文本不会在保存时执行，依赖变量或设备的有效性仍由步骤校验与执行处理。
