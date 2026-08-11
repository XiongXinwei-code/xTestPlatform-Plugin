using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP.Executors;

public sealed class XcpConnectExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new XcpConnectPlugin().CreateSerializer();
        var setting = (XcpConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await XcpExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var response = await client.ConnectAsync(setting.ConnectMode, cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"XCP CONNECT 成功: MaxCTO={response.MaxCto}, MaxDTO={response.MaxDto}, " +
                    $"CAL={response.SupportsCalibration}, DAQ={response.SupportsDaq}, PGM={response.SupportsProgramming}");

            if (!string.IsNullOrWhiteSpace(setting.ResourceVariable))
                context.Resources.Set(setting.ResourceVariable, response.ResourceMask);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = $"MaxCTO={response.MaxCto} MaxDTO={response.MaxDto} Resource=0x{response.ResourceMask:X2}"
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error  = new ErrorInfo { Message = $"XCP CONNECT 失败: {ex.Message}" }
                }
            };
        }
    }
}
