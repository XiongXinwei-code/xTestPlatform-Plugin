using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using CAN.Validation;

namespace CAN;

public sealed class CanReadPlugin : StepPluginBase<CanReadSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.CanRead";
    public override string DisplayName => "CAN_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        从已打开的 CAN 通道接收一帧报文，可按 ID 过滤，结果存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | FilterId | string([ExpressionField]) | 否 | 空 | 过滤 CAN ID，如 0x7E8，为空则接收任意帧 |
        | ReadTimeoutMs | int | 否 | — | 读取超时毫秒数 |
        | ResultVariable | string(变量路径) | 是 | — | 结果变量名，写入类型为 string（十六进制报文数据） |
        | IdVariable | string(变量路径) | 否 | 空 | 接收帧 CAN ID 存入的变量名 |
        | EnableLog | bool | 否 | true | 是否输出读取日志 |

        ## 行为

        - 超时未收到匹配报文时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Write`：发送报文
        """;

    public override IStepExecutor CreateExecutor() => new CanReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var filter = string.IsNullOrWhiteSpace(s.FilterId) ? "Any" : s.FilterId;
        return $"Read {s.ConnectionName} Filter={filter} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (CanReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_050", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("CAN_050E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
            errors.Add(StepSettingError.Error("CAN_051", "超时必须大于 0，或为 -1 表示永不超时"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("CAN_W50", "未配置结果变量，数据将不会存储"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("CAN_W51", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not string)
                errors.Add(StepSettingError.Error("CAN_W52", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
        }
        if (!string.IsNullOrWhiteSpace(s.IdVariable))
        {
            if (!context.ExecutionContext.HasVariable(s.IdVariable))
                errors.Add(StepSettingError.Error("CAN_W53", $"IdVariable {s.IdVariable} 不存在，请先创建该变量"));
            else
            {
                var idVal = context.ExecutionContext.GetVariable(s.IdVariable);
                if (idVal is not null && idVal is not string)
                    errors.Add(StepSettingError.Error("CAN_W54", $"IdVariable {s.IdVariable} 类型不匹配，期望 string，实际类型 {idVal.GetType().Name}"));
            }
        }
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
