using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 读取节点执行器</summary>
public sealed class OpcUaReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<NodeId, CancellationToken, Task<DataValue>>? _read;
    public OpcUaReadExecutor() { }
    internal OpcUaReadExecutor(Func<NodeId, CancellationToken, Task<DataValue>> read) => _read = read;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaReadPlugin().CreateSerializer();
            var setting = (OpcUaReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            guard.Check();
            var nodeIdStr = setting.NodeId;
            var key = OpcUaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<OpcUaConnectionResource>(key, out var connection))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"OPC UA 连接 {connName} 不存在，请先执行 OpcUa_Connect" }
                    }
                };
            }

            var nodeId = OpcUaHelper.ParseNodeId(nodeIdStr);
            guard.EnsureCurrent(key, connection);
            var dataValue = await connection.RunAsync(token => { guard.EnsureCurrent(key, connection); return _read?.Invoke(nodeId, token) ?? connection.Session.ReadValueAsync(nodeId, token); },
                setting.TimeoutMs, "OPC UA 单点读取", cancellationToken);
            guard.EnsureCurrent(key, connection);

            if (StatusCode.IsBad(dataValue.StatusCode))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"读取节点失败: {dataValue.StatusCode}" }
                    }
                };
            }

            var value = dataValue.Value?.ToString() ?? "";
            guard.EnsureCurrent(key, connection);
            context.SetVariable(setting.ResultVariable, dataValue.Value);
            context.Log($"OPC UA 读取: {nodeIdStr} = {value}");
            guard.EnsureCurrent(key, connection);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = value }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
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
                    Error = ErrorInfo.FromException(ex, $"OPC UA 读取失败: {ex.Message}")
                }
            };
        }
    }
}
