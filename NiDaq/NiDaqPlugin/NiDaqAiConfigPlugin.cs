using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAiConfigPlugin : StepPluginBase<NiDaqAiConfigSetting>
{
    public override string StepTypeId => "NiDaq.AiConfig";
    public override string DisplayName => "NiDaq_AI_Config";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        配置 NI DAQ AI 模拟输入采集任务（通道、终端、电压范围、时钟、触发），创建任务对象供后续 Start/Read 使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | "AiTask1" | 任务名称，求值结果为 string |
        | Channels | 集合 | 是 | — | AI 通道列表，元素字段见下方 |
        | SampleRate | double | 是 | 1000 | 采样率 Hz |
        | SamplesPerChannel | int | 是 | 100 | 每通道采样数 |
        | SampleMode | 枚举 | 否 | FiniteSamples | 可选值：FiniteSamples, ContinuousSamples |
        | ClockSource | string | 否 | 空 | 时钟源，空为内部时钟 |
        | UseTrigger | bool | 否 | false | 是否使用触发；TriggerSource 为空时不配置触发 |
        | TriggerSource | string | 否 | 空 | 触发源 |
        | TriggerEdge | 枚举 | 否 | Rising | 可选值：Rising, Falling |

        Channels 元素字段：

        - PhysicalChannel：string，物理通道（普通文本，如 Dev1/ai0）
        - ColumnName：string，该通道在采集数据中的列名（如 CH1）
        - MinValue / MaxValue：double，量程下限/上限，默认 -10 / 10
        - Terminal：枚举，终端配置，可选值：Differential, RSE, NRSE, Pseudodifferential，默认 Differential

        ## 物理通道命名规则

        格式为 `<设备名>/<通道>`，设备名在 NI MAX 中查看（默认 Dev1、Dev2…）：

        - 单个 AI 通道：`Dev1/ai0`
        - 连续通道范围：`Dev1/ai0:3`（表示 ai0~ai3 共 4 通道）
        - 多个不连续通道：`Dev1/ai0,Dev1/ai2,Dev1/ai5`（逗号分隔）
        - 时钟/触发源使用 PFI 端子时需带前导斜杠：`/Dev1/PFI0`

        ## 行为

        - 仅创建任务，不启动采集
        - 已存在同名任务时先释放旧任务再创建
        - Channels 为空或 NI 驱动不可用时步骤报错

        ## 相关插件

        - `NiDaq_Task_Start`：启动任务
        - `NiDaq_AI_Read`：读取采集数据
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqAiConfigExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Config: {s.TaskName} ({s.Channels.Count} ch, {s.SampleRate} Hz)";
    }
}
