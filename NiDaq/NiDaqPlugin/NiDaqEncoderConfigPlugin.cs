using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqEncoderConfigPlugin : StepPluginBase<NiDaqEncoderConfigSetting>
{
    public override string StepTypeId => "NiDaq.EncoderConfig";
    public override string DisplayName => "NiDaq_Encoder_Config";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_encoder_config.png";

    public override string Description => """
        ## 功能

        配置 NI DAQ 编码器采集任务（Counter 通道、解码类型、脉冲数、单位），创建任务对象供后续 Start/Read 使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 任务名称，求值结果为 string |
        | CounterChannel | string([ExpressionField]) | 是 | — | Counter 通道，如 Dev1/ctr0，求值结果为 string |
        | DecodingType | 枚举 | 否 | X4 | 可选值：X1, X2, X4 |
        | PulsesPerRevolution | int | 否 | 1024 | 每转脉冲数 PPR |
        | ZIndexEnable | bool | 否 | false | 是否启用 Z 索引复位 |
        | DistancePerPulse | double | 否 | 0.3515625 | 每脉冲对应的距离/角度 |
        | Unit | 枚举 | 否 | Degrees | 可选值：Pulses, Degrees, Millimeters |
        | TimeoutMs | int | 否 | 10000 | 名称/通道求值后的配置阶段总期限（毫秒），包含登记、旧资源收尾及 SDK 配置；不包含表达式求值 |

        ### 物理通道命名规则

        格式为 `<设备名>/ctr<计数器号>`，设备名在 NI MAX 中查看（默认 Dev1、Dev2…）：

        - 计数器通道：`Dev1/ctr0`、`Dev1/ctr1`（每个编码器占用一个计数器）
        - 编码器 A/B/Z 相实际接线端子由计数器默认路由决定，可在 NI MAX 中查看

        ## 行为


        - 建立前先登记当前 owner，关闭请求可以冻结建立期资源；同名活动资源明确拒绝覆盖。同一插件上次执行的同物理资源正在收尾时，在本步骤期限内等待，真实退出前不再次占用设备。

        - 仅创建任务，不启动采集

        ## 相关插件

        - `NiDaq_Task_Start`：启动任务
        - `NiDaq_Encoder_Read`：读取编码器位置
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new NiDaqEncoderConfigExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Encoder Config: {s.TaskName} ({s.CounterChannel}, {s.DecodingType})";
    }
}
