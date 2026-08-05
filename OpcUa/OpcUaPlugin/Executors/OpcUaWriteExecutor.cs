using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 写入节点执行器</summary>
public sealed class OpcUaWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaWritePlugin().CreateSerializer();
        var setting = (OpcUaWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var nodeIdStr = await Evaluator.EvalStringAsync(setting.NodeId, context);
            var writeValueStr = await Evaluator.EvalStringAsync(setting.WriteValue, context);
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

            // 如果是 Auto 类型，先读取节点的数据类型
            object writeValue;
            if (setting.DataType == OpcUaDataType.Auto)
            {
                var currentValue = await session.ReadValueAsync(nodeId, cancellationToken);
                if (currentValue.Value != null)
                {
                    writeValue = Convert.ChangeType(writeValueStr, currentValue.Value.GetType());
                }
                else
                {
                    writeValue = writeValueStr;
                }
            }
            else
            {
                writeValue = OpcUaHelper.ConvertValue(writeValueStr, setting.DataType);
            }

            var writeValueCollection = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(writeValue))
                }
            };

            var response = await session.WriteAsync(null, writeValueCollection, cancellationToken);

            if (StatusCode.IsBad(response.Results[0]))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"写入节点失败: {response.Results[0]}" }
                    }
                };
            }

            context.LogAction?.Invoke($"OPC UA 写入: {nodeIdStr} = {writeValueStr}");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = writeValueStr }
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
                    Error = new ErrorInfo { Message = $"OPC UA 写入失败: {ex.Message}" }
                }
            };
        }
    }
}
