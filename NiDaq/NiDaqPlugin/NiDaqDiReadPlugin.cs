using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDiReadPlugin : StepPluginBase<NiDaqDiReadSetting>, IStepPlugin
{
    public override string StepTypeId => "NiDaq.DiRead";
    public override string DisplayName => "NiDaq_DI_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        读取 NI DAQ 数字输入通道的状态值，存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Channel | string([ExpressionField]) | 是 | — | 物理通道，如 Dev1/port0/line0:7 |
        | ResultVariable | string(变量路径) | 是 | — | 结果变量名，写入类型为 uint（端口状态值） |

        ## 物理通道命名规则

        格式为 `<设备名>/port<端口号>/line<线号>`，设备名在 NI MAX 中查看（默认 Dev1、Dev2…）：

        - 单条数字线：`Dev1/port0/line0`
        - 连续线范围：`Dev1/port0/line0:7`（line0~line7 共 8 条线）
        - 整个端口：`Dev1/port0`

        ## 行为

        - 单次读取，无需预先配置任务

        ## 相关插件

        - `NiDaq_DO_Write`：设置数字输出
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqDiReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DI Read: {s.Channel} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (NiDaqDiReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.Channel)) errors.Add(StepSettingError.Error("DAQ_090", "物理通道不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.Channel, context.ExecutionContext, out var chErr))
            errors.Add(StepSettingError.Error("DAQ_090E", $"Channel 表达式无效: {chErr}"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_091", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_092", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not uint)
                errors.Add(StepSettingError.Error("DAQ_093", $"变量 {s.ResultVariable} 类型不匹配，期望 uint，实际类型 {val.GetType().Name}"));
        }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
