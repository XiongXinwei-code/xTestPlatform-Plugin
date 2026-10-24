using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS.Executors;

public sealed class UdsReadDtcExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new UdsReadDtcPlugin().CreateSerializer();
            var setting = (UdsReadDtcSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken, guard);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var request = new byte[] { 0x19, setting.SubFunction, setting.StatusMask };
            var response = await guard.RunAsync(() => client.RequestAsync(request, cancellationToken));

            if (response.IsPositive)
            {
                var hex = UdsExecutorHelper.ToHex(response.Data);
                if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                    guard.SetVariable(setting.ResultVariable, hex);
                guard.Log($"UDS ReadDTC: SubFunc=0x{setting.SubFunction:X2}, Data=[{hex}]");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = hex } };
            }
            else
            {
                return UdsExecutorHelper.Failure($"UDS 读取 DTC，子功能 0x{setting.SubFunction:X2}", response);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return ExecutionResults.Aborted(); }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) } }; }
    }
}
