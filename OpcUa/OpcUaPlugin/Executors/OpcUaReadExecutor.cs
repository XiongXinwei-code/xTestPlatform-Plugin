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

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaReadPlugin().CreateSerializer();
        var setting = (OpcUaReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
            var nodeIdStr = await Evaluator.EvaluateAsync<string>(setting.NodeId, context) ?? setting.NodeId;
            var key = OpcUaHelper.GetSessionKey(connName);

            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not Session session)
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
            var dataValue = await session.ReadValueAsync(nodeId, cancellationToken);

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
            context.SetVariable(setting.ResultVariable, dataValue.Value);
            context.LogAction?.Invoke($"OPC UA 读取: {nodeIdStr} = {value}");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = value }
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
                    Error = new ErrorInfo { Message = $"OPC UA 读取失败: {ex.Message}" }
                }
            };
        }
    }
}
