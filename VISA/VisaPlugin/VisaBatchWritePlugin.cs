using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using VISA.Validation;

namespace VISA;

/// <summary>
/// VISA 批量写入插件，按顺序发送多条 SCPI 命令，支持命令间延时
/// </summary>
public sealed class VisaBatchWritePlugin : StepPluginBase<VisaBatchWriteSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.VisaBatchWrite";
    public override string DisplayName => "VISA_BatchWrite";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        批量发送多条 SCPI 命令到 VISA 仪器，按顺序逐条发送，每条命令发送后可指定延时等待。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 VISA 连接标识名 |
        | Items | 集合 | 是 | — | 命令列表，元素结构见示例 |

        Items 元素字段：Command(string([ExpressionField]), SCPI 命令)，DelayMs(int, 发送后延时毫秒，0 表示不延时)。

        ## 行为

        - 按列表顺序逐条发送，每条发送后等待 DelayMs 毫秒
        - 任意一条发送失败则步骤报错并停止后续发送

        ## 示例

        ```json
        {
          "ConnectionName": "\"VISA1\"",
          "Items": [
            { "Command": "\"*RST\"", "DelayMs": 100 },
            { "Command": "\":CONF:VOLT:DC\"", "DelayMs": 0 }
          ]
        }
        ```

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Write`：发送单条命令
        """;

    public override IStepExecutor CreateExecutor() => new VisaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.ConnectionName}: {s.Items.Count} 条命令";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (VisaBatchWriteSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_060", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("VISA_060E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Error("VISA_061", "至少需要一条 SCPI 命令"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            if (s.Items[i].DelayMs < 0)
                errors.Add(StepSettingError.Error("VISA_063", $"第 {i + 1} 条命令：延时不能为负数"));
        }
        for (int i = 0; i < s.Items.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(s.Items[i].Command))
                errors.Add(StepSettingError.Error("VISA_062", $"第 {i + 1} 行：命令不能为空"));
            else if (!context.Evaluator.ValidateExpression(s.Items[i].Command, context.ExecutionContext, out var cmdErr))
                errors.Add(StepSettingError.Error("VISA_062E", $"第 {i + 1} 行：Command 表达式无效: {cmdErr}"));
        }
        VisaLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
