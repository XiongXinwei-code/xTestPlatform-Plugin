# NiDaq 插件

## 功能概述

基于 NI-DAQmx 进行数据采集与信号输出，支持模拟量采集、数字量读写、编码器计数以及多任务同步采集。

## 支持的硬件/协议

- NI 数据采集卡（USB / PCIe / cDAQ 等，需 NI-DAQmx 驱动）

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| NiDaq_AI_Config | 配置 NI DAQ AI 模拟输入采集任务（通道、终端、电压范围、时钟、触发），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_AI_Read | 从已启动的 AI 采集任务中读取数据，可导出为文件并/或将结果存入变量。 |
| NiDaq_DI_Read | 读取 NI DAQ 数字输入通道的状态值，存入变量。 |
| NiDaq_DO_Write | 设置 NI DAQ 数字输出通道的状态值。 |
| NiDaq_Encoder_Config | 配置 NI DAQ 编码器采集任务（Counter 通道、解码类型、脉冲数、单位），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_Encoder_Read | 从已配置的编码器任务中读取当前位置值，存入指定变量。 |
| NiDaq_Sync_Config | 配置 NI DAQ 同步采集任务（AI 通道 + 编码器通道、共享时钟/触发），创建任务对象供后续 Start/Read 使用。 |
| NiDaq_Sync_Read | 从已启动的同步采集任务中读取 AI 和编码器对齐数据，可导出为文件并/或将结果存入变量。 |
| NiDaq_Task_Start | 启动已配置的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。 |
| NiDaq_Task_Stop | 停止并释放已启动的 NI DAQ 采集任务（通用，适用于 AI/编码器/同步任务）。 |

## 使用前提

需安装 NI-DAQmx 驱动，并在 NI MAX 中确认设备名称。
