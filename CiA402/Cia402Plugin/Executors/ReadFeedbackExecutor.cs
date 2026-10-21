using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class ReadFeedbackExecutor : AxisActionExecutor<ReadFeedbackSetting>
{
    protected override bool IsControl => false;
    protected override async Task OperateAsync(ReadFeedbackSetting setting, EvaluatedSettings values, AxisSession session, IExecutionContext context, CancellationToken ct)
    {
        var feedback = await session.Controller.Transport.ReadFeedbackAsync(!string.IsNullOrWhiteSpace(setting.PositionVariable), !string.IsNullOrWhiteSpace(setting.VelocityVariable), !string.IsNullOrWhiteSpace(setting.TorqueVariable), ct).ConfigureAwait(false);
		SettingValidation.Output(context, setting.StatusWordVariable, (double)feedback.StatusWord);
		if (feedback.Position.HasValue) SettingValidation.Output(context, setting.PositionVariable, (double)feedback.Position.Value);
		if (feedback.Velocity.HasValue) SettingValidation.Output(context, setting.VelocityVariable, (double)feedback.Velocity.Value);
		if (feedback.Torque.HasValue) SettingValidation.Output(context, setting.TorqueVariable, (double)feedback.Torque.Value);
		context.Log($"反馈状态字 0x{feedback.StatusWord:X4}");
    }
}
