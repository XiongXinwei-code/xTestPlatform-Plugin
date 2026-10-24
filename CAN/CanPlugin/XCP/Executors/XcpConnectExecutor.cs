using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP.Executors;

public sealed class XcpConnectExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new XcpConnectPlugin().CreateSerializer();
            var setting = (XcpConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var (client, error) = await XcpExecutorHelper.CreateClientAsync(setting, context, cancellationToken, guard);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var response = await guard.RunAsync(() => client.ConnectAsync(setting.ConnectMode, cancellationToken));

            guard.Log(
                $"XCP CONNECT 成功: MaxCTO={response.MaxCto}, MaxDTO={response.MaxDto}, " +
                $"CAL={response.SupportsCalibration}, DAQ={response.SupportsDaq}, PGM={response.SupportsProgramming}");

            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(setting.ResourceVariable))
                guard.SetVariable(setting.ResourceVariable, response.ResourceMask);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value  = $"MaxCTO={response.MaxCto} MaxDTO={response.MaxDto} Resource=0x{response.ResourceMask:X2}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                    Error  = ErrorInfo.FromException(ex, $"XCP CONNECT 失败: {ex.Message}")
                }
            };
        }
    }
}
