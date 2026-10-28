using Ethernet.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpOpenExecutor : IStepExecutor
{
    private readonly Func<string, IExecutionContext, Task<string>> _evaluate;
    public TcpOpenExecutor() : this(EthernetExecutorHelper.EvalStringAsync) { }
    internal TcpOpenExecutor(Func<string, IExecutionContext, Task<string>> evaluate) => _evaluate = evaluate;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {

        string? host = null;
        var port = 0;
        int timeoutMs = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Ethernet");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                execution.ThrowIfExpired();
            }
            var step = context.CurrentStep!.Step;
            var serializer = new TcpOpenPlugin().CreateSerializer();
            var setting = (TcpOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            timeoutMs = setting.ConnectTimeoutMs;
            host = await _evaluate(setting.RemoteHost, context);
            CheckExecution();
            var portStr = await _evaluate(setting.RemotePort, context);
            CheckExecution();
            var name = await _evaluate(setting.ConnectionName, context);
            CheckExecution();

            if (!int.TryParse(portStr, out port))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"TCP 连接失败: 端口号 '{portStr}' 无效" }
                    }
                };

            var client = await TcpConnectionManager.ConnectAsync(name!, host!, port, setting.ConnectTimeoutMs, cancellationToken, context.Resources, execution.ThrowIfExpired);

            context.Log($"TCP 连接成功: {name} -> {host}:{port}");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (!ReferenceEquals(client, TcpConnectionManager.Get(name!, context.Resources)))
                throw new InvalidOperationException("TCP 刚建立的连接已被关闭或替换，不能报告旧代次成功");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"TCP 已连接: {name} -> {host}:{port}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"TCP 连接超时({timeoutMs}ms): {host}:{port}")
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
                    Error = ErrorInfo.FromException(ex, $"TCP OPEN 失败: {ex.Message}")
                }
            };
        }
    }
}
