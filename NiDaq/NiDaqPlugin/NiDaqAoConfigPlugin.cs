using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAoConfigPlugin : StepPluginBase<NiDaqAoConfigSetting>
{
    public override string StepTypeId => "NiDaq.AoConfig";
    public override string DisplayName => "NiDaq_AO_Config";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ao_config.png";
    public override string Description => """
        ## 功能

        配置同一 NI 设备/模块的按需 AO 电压单点输出任务。此步骤验证通道和配置，但不调用电压写入；配置/预留阶段的电气行为由设备定义。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | "AoTask1" | 求值结果为 string，后续 Start/WriteSingle/Stop 使用相同任务名 |
        | Channels | 集合 | 是 | 空 | 1~256 行，同一设备/模块，每行一个物理 AO 通道；按行序匹配电压 |
        | InitialValues | string([ExpressionField]) | 是 | 空 | 求值结果为数值或数值集合（如 new double[] { 1.0, 2.0 }），单位 V；配置时复制，TaskStart 时写入 |
        | StopPolicy | 枚举 | 是 | Unspecified | 必须明确选择 DeviceDefined 或 WriteFinalValues；不假定 0 V 安全 |
        | FinalValues | string([ExpressionField]) | 条件必填 | 空 | WriteFinalValues 时必填，求值结果为数值或数值集合，单位 V；配置时复制，停止/中断回收时使用 |
        | CleanupTimeoutMs | int | 否 | 5000 | AO 真实收尾总期限 ms，包含等待原始 I/O 退出、终值写入及 Stop；大于 0 |
        | TimeoutMs | int | 否 | 10000 | 配置总等待期限 ms，包含表达式求值、旧资源收尾、SDK 创建/验证；大于 0 |

        Channels 元素：PhysicalChannel（普通文本，如 Dev1/ao0；不接受范围/逗号列表）、ColumnName（可选唯一通道名）、MinValue / MaxValue（有限电压量程，默认 -10 / 10 V，下限小于上限）。设备实际支持的通道与量程仍须通过 DAQmx 验证。

        ## 行为与生命周期

        - 先登记 owner，再创建 SDK 任务；同名任务拒绝覆盖。配置不会启动输出，也不安装波形/采样时钟。
        - `NiDaq_Task_Start` 显式启动并写入初值；`NiDaq_AO_WriteSingle` 不会隐式启动。全部数值有限、数量匹配且在各通道量程内，拒绝截断。
        - DeviceDefined 表示接受设备 Stop/释放后的行为，不保证保持末值、高阻或安全电压；WriteFinalValues 在停止前写入指定终值。
        - 中断/回收先冻结新写入，等真实 writer 退出后串行收尾；等待超时保留 owner/句柄和隔离。终值写入结果未知时禁止盲重发，应核实设备输出及日志。
        - TaskStop 的等待期限与本配置的真实收尾期限分别生效；SDK 释放可继续后台执行，真实退出前不能抢占同一 AO 引擎。
        - SDK 返回仅表示命令调用结果，不表示测得电压、断电安全或电机停稳；多通道单点不承诺电气同时更新。

        ## 相关插件

        - `NiDaq_Task_Start`、`NiDaq_AO_WriteSingle`、`NiDaq_Task_Stop`。
        """;
    public override IStepExecutor CreateExecutor() => new NiDaqAoConfigExecutor();
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AO Config: {s.TaskName} ({s.Channels.Count} ch, Stop={s.StopPolicy}, Cleanup={s.CleanupTimeoutMs} ms)";
    }
}
