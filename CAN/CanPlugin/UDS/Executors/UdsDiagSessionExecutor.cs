using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS.Executors;

public sealed class UdsDiagSessionExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new UdsDiagSessionPlugin().CreateSerializer();
            var setting = (UdsDiagSessionSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken, guard);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            byte subFunc = (byte)setting.SessionType;
            if (setting.SuppressPositiveResponse)
                subFunc |= 0x80;

            var request = new byte[] { 0x10, subFunc };

            if (setting.SuppressPositiveResponse)
            {
                await guard.RunAsync(() => client.SendOnlyAsync(request, cancellationToken));
                guard.Log($"UDS DiagSession: 0x{(byte)setting.SessionType:X2} (抑制正响应)");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = setting.SessionType.ToString() } };
            }

            var response = await guard.RunAsync(() => client.RequestAsync(request, cancellationToken));

            if (response.IsPositive)
            {
                guard.Log($"UDS DiagSession 切换成功: {setting.SessionType}");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = setting.SessionType.ToString() } };
            }
            else
            {
                return UdsExecutorHelper.Failure($"UDS 切换会话 {setting.SessionType}", response, response.GetFailureValue());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) } };
        }
    }
}
