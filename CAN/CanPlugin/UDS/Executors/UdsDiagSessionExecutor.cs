using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS.Executors;

public sealed class UdsDiagSessionExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsDiagSessionPlugin().CreateSerializer();
        var setting = (UdsDiagSessionSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            byte subFunc = (byte)setting.SessionType;
            if (setting.SuppressPositiveResponse)
                subFunc |= 0x80;

            var request = new byte[] { 0x10, subFunc };

            if (setting.SuppressPositiveResponse)
            {
                await client.SendOnlyAsync(request, cancellationToken);
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS DiagSession: 0x{(byte)setting.SessionType:X2} (抑制正响应)");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = setting.SessionType.ToString() } };
            }

            var response = await client.RequestAsync(request, cancellationToken);

            if (response.IsPositive)
            {
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS DiagSession 切换成功: {setting.SessionType}");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = setting.SessionType.ToString() } };
            }
            else
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Error = new ErrorInfo { Message = $"否定响应: {response.GetNrcDescription()}" },
                        Value = $"NRC=0x{response.NegativeResponseCode:X2}"
                    }
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } };
        }
    }
}
