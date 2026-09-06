using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDoWritePlugin : StepPluginBase<NiDaqDoWriteSetting>, IStepPlugin
{
    public override string StepTypeId => "NiDaq.DoWrite";
    public override string DisplayName => "NiDaq_DO_Write";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        设置 NI DAQ 数字输出通道的状态值。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Channel | string([ExpressionField]) | 是 | — | 物理通道，如 Dev1/port0/line0 |
        | Value | string([ExpressionField]) | 是 | — | 输出值，true/false 或 byte |

        ## 物理通道命名规则

        格式为 `<设备名>/port<端口号>/line<线号>`，设备名在 NI MAX 中查看（默认 Dev1、Dev2…）：

        - 单条数字线：`Dev1/port0/line0`
        - 连续线范围：`Dev1/port0/line0:7`（line0~line7 共 8 条线）
        - 整个端口：`Dev1/port0`

        ## 行为

        - 单次写入，无需预先配置任务

        ## 相关插件

        - `NiDaq_DI_Read`：读取数字输入
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqDoWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DO Write: {s.Channel} = {s.Value}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (NiDaqDoWriteSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.Channel)) errors.Add(StepSettingError.Error("DAQ_100", "物理通道不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Channel, context.ExecutionContext, out var chErr))
            errors.Add(StepSettingError.Error("DAQ_100E", $"Channel 表达式无效: {chErr}"));
        if (string.IsNullOrWhiteSpace(s.Value)) errors.Add(StepSettingError.Error("DAQ_101", "输出值不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Value, context.ExecutionContext, out var valErr))
            errors.Add(StepSettingError.Error("DAQ_101E", $"Value 表达式无效: {valErr}"));
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
