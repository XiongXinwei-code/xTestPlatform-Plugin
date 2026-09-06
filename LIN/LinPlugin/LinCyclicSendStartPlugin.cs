using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using LIN.Validation;

namespace LIN;

public sealed class LinCyclicSendStartPlugin : StepPluginBase<LinCyclicSendStartSetting>, IStepPlugin
{
    public override string StepTypeId   => "IO.LinCyclicSendStart";
    public override string DisplayName  => "LIN_Cyclic_SendStart";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        启动 LIN 周期发送任务，在后台按各帧配置的周期持续发送多个 LIN 帧，直到执行 LIN_Cyclic_SendStop 停止。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名 |
        | TaskName | string([ExpressionField]) | 是 | "LinCyclicTask1" | 任务标识名，Stop 步骤用此名称停止 |
        | EnableLog | bool | 否 | false | 是否输出发送日志 |
        | Frames | 集合 | 是 | — | 周期发送帧列表，元素结构见下方示例 |

        Frames 元素 JSON 示例：

        ```json
        {"FrameId":"0","Data":"\"FF FF FF FF FF FF FF FF\"","CycleTimeMs":100,"ChecksumType":"Enhanced","Enabled":true}
        ```

        - ChecksumType 可选值：Classic, Enhanced
        - FrameId 和 Data 为表达式字段，字面量字符串需用引号包裹

        ## 行为

        - 任务在后台运行，重名任务启动会报错

        ## 相关插件

        - `LIN_Cyclic_SendStop`：停止周期发送任务
        - `LIN_Open`：打开 LIN 通道
        """;

    public override IStepExecutor CreateExecutor() => new LinCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStart TaskName={s.TaskName}, 帧数={s.Frames.Count}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (LinCyclicSendStartSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_CS01", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("LIN_CS02", "任务标识名不能为空"));
        if (s.Frames.Count == 0)
            errors.Add(StepSettingError.Warning("LIN_CS03", "帧列表为空，周期发送将不会发送任何数据"));

        foreach (var frame in s.Frames.Where(f => f.Enabled))
        {
            if (frame.CycleTimeMs <= 0)
                errors.Add(StepSettingError.Error("LIN_CS04", $"帧 {frame.FrameId} 的周期时间必须大于 0"));
        }

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
