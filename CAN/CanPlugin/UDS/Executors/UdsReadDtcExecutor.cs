using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS.Executors;

public sealed class UdsReadDtcExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsReadDtcPlugin().CreateSerializer();
        var setting = (UdsReadDtcSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var request = new byte[] { 0x19, setting.SubFunction, setting.StatusMask };
            var response = await client.RequestAsync(request, cancellationToken);

            if (response.IsPositive)
            {
                var hex = UdsExecutorHelper.ToHex(response.Data);
                if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                    context.SetVariable(setting.ResultVariable, hex);
                context.LogAction?.Invoke($"UDS ReadDTC: SubFunc=0x{setting.SubFunction:X2}, Data=[{hex}]");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = hex } };
            }
            else
            {
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = response.GetNrcDescription() } } };
            }
        }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } }; }
    }
}
