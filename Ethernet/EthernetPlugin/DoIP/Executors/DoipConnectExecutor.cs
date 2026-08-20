using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipConnectExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new DoipConnectPlugin().CreateSerializer();
        var setting = (DoipConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        string? host = null;
        var port = 0;
        try
        {
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.SessionName, context);
            host = await EthernetExecutorHelper.EvalStringAsync(setting.RemoteHost, context);
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.RemotePort, context);

            if (!int.TryParse(portStr, out port))
                return Error($"DoIP 连接失败: 端口号 [{portStr}] 无效");

            var sourceAddress = DoipHelper.ParseAddress(setting.SourceAddress);
            var activationType = DoipHelper.ToActivationByte(setting.ActivationType);

            await DoipConnectionManager.ConnectAsync(
                name, host, port, sourceAddress, activationType, setting.TimeoutMs, cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"DoIP 连接并路由激活成功: {name} -> {host}:{port}, 源地址=0x{sourceAddress:X4}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"DoIP 已连接: {name} -> {host}:{port}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException)
        {
            return Error($"DoIP 连接超时({setting.TimeoutMs}ms): {host}:{port}");
        }
        catch (Exception ex)
        {
            return Error($"DoIP CONNECT 失败: {ex.Message}", ex);
        }
    }

    private static ExecutionResult Error(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
