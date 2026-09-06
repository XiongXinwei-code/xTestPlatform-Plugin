using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using LIN.Validation;

namespace LIN;

public sealed class LinWritePlugin : StepPluginBase<LinWriteSetting>, IStepPlugin
{
    public override string StepTypeId   => "IO.LinWrite";
    public override string DisplayName  => "LIN_Write";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        向 LIN 总线发送一帧数据（主节点发送帧头和数据）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名 |
        | FrameId | string([ExpressionField]) | 是 | 0 | 帧 ID 0-63 |
        | Data | string([ExpressionField]) | 是 | 空 | 十六进制数据，如 "01 02 03" |
        | ChecksumType | 枚举 | 否 | Enhanced | 可选值：Classic, Enhanced |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 需先通过 LIN_Open 以主节点模式打开通道

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Read`：接收 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => new LinWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write ID={s.FrameId} Data={s.Data} ({s.ChecksumType})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (LinWriteSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_W01", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.FrameId))
            errors.Add(StepSettingError.Error("LIN_W02", "帧 ID 不能为空"));

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
