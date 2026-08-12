using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpOpenExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new TcpOpenPlugin().CreateSerializer();
        var setting = (TcpOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        string? host = null;
        var port = 0;
        try
        {
            host = await EthernetExecutorHelper.EvalStringAsync(setting.RemoteHost, context);
            var portStr = await EthernetExecutorHelper.EvalStringAsync(setting.RemotePort, context);
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);

            if (!int.TryParse(portStr, out port))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"TCP 连接失败: 端口号 '{portStr}' 无效" }
                    }
                };

            await TcpConnectionManager.ConnectAsync(name!, host!, port, setting.ConnectTimeoutMs, cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"TCP 连接成功: {name} -> {host}:{port}");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"TCP 已连接: {name} -> {host}:{port}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"TCP 连接超时({setting.ConnectTimeoutMs}ms): {host}:{port}" }
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"TCP OPEN 失败: {ex.Message}" }
                }
            };
        }
    }
}
