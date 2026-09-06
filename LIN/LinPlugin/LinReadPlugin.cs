using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using LIN.Validation;

namespace LIN;

public sealed class LinReadPlugin : StepPluginBase<LinReadSetting>, IStepPlugin
{
    public override string StepTypeId   => "IO.LinRead";
    public override string DisplayName  => "LIN_Read";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        从 LIN 总线接收一帧数据，可按帧 ID 过滤，结果存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名 |
        | FilterFrameId | string([ExpressionField]) | 否 | 空 | 过滤帧 ID 0-63，空则接收任意帧 |
        | ReadTimeoutMs | int | 否 | 1000 | 读取超时毫秒数 |
        | ResultVariable | string(变量路径) | 是 | — | 结果变量名，写入类型为 string（十六进制数据） |
        | IdVariable | string(变量路径) | 否 | 空 | 存储帧 ID 的变量路径 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时未收到匹配帧时步骤报错

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Write`：发送 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => new LinReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var filter = string.IsNullOrWhiteSpace(s.FilterFrameId) ? "任意ID" : $"ID={s.FilterFrameId}";
        return $"Read {filter}, Timeout={s.ReadTimeoutMs}ms";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (LinReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_R01", "连接标识名不能为空"));
        if (s.ReadTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("LIN_R02", "读取超时时间必须大于 0"));

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
