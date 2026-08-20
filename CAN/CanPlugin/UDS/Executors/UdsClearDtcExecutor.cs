using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS.Executors;

public sealed class UdsClearDtcExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsClearDtcPlugin().CreateSerializer();
        var setting = (UdsClearDtcSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var groupStr = await Evaluator.EvaluateAsync<string>(setting.DtcGroup, context) ?? setting.DtcGroup;
            uint group = UdsExecutorHelper.ParseId(groupStr);

            var request = new byte[] { 0x14, (byte)(group >> 16), (byte)(group >> 8), (byte)(group & 0xFF) };
            var response = await client.RequestAsync(request, cancellationToken);

            if (response.IsPositive)
            {
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS ClearDTC: 清除成功 (Group=0x{group:X6})");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = $"0x{group:X6}" } };
            }
            else
            {
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = response.GetNrcDescription() } } };
            }
        }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) } }; }
    }
}
