using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipConnectExecutor : IStepExecutor
{
    private readonly Func<string, IExecutionContext, Task<string>> _evaluate;
    public DoipConnectExecutor() : this(EthernetExecutorHelper.EvalStringAsync) { }
    internal DoipConnectExecutor(Func<string, IExecutionContext, Task<string>> evaluate) => _evaluate = evaluate;

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
            var serializer = new DoipConnectPlugin().CreateSerializer();
            var setting = (DoipConnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            timeoutMs = setting.TimeoutMs;
            var name = await _evaluate(setting.SessionName, context);
            CheckExecution();
            host = await _evaluate(setting.RemoteHost, context);
            CheckExecution();
            var portStr = await _evaluate(setting.RemotePort, context);
            CheckExecution();

            if (!int.TryParse(portStr, out port))
                return Error($"DoIP 连接失败: 端口号 [{portStr}] 无效");

            var sourceAddress = DoipHelper.ParseAddress(setting.SourceAddress);
            var activationType = DoipHelper.ToActivationByte(setting.ActivationType);

            var client = await DoipConnectionManager.ConnectAsync(
                name, host, port, sourceAddress, activationType, setting.TimeoutMs, cancellationToken, context.Resources, execution.ThrowIfExpired);

            context.Log(
                $"DoIP 连接并路由激活成功: {name} -> {host}:{port}, 源地址=0x{sourceAddress:X4}");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (!ReferenceEquals(client, DoipConnectionManager.Get(name!, context.Resources)))
                throw new InvalidOperationException("DoIP 刚建立的会话已被关闭或替换，不能报告旧代次成功");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"DoIP 已连接: {name} -> {host}:{port}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException ex)
        {
            return Error($"DoIP 连接超时({timeoutMs}ms): {host}:{port}", ex);
        }
        catch (Exception ex)
        {
            return Error($"DoIP 连接 {host}:{port} 失败：{ex.Message}", ex);
        }
    }

    private static ExecutionResult Error(string message, Exception? ex = null)
        => ex is null ? ExecutionResults.Error(message) : ExecutionResults.FromException(ex, message);
}
