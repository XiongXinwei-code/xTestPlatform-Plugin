using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 批量写入执行器</summary>
public sealed class OpcUaBatchWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaBatchWritePlugin().CreateSerializer();
        var setting = (OpcUaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = OpcUaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<Session>(key, out var session))
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

            if (setting.Items.Count == 0)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Passed, Value = "无节点需要写入" }
                };
            }

            // 构建写入请求
            var writeValues = new WriteValueCollection();
            foreach (var item in setting.Items)
            {
                var nodeIdStr = await Evaluator.EvalStringAsync(item.NodeId, context);
                var writeValueStr = await Evaluator.EvalStringAsync(item.WriteValue, context);
                var nodeId = OpcUaHelper.ParseNodeId(nodeIdStr);

                object writeValue;
                if (item.DataType == OpcUaDataType.Auto)
                {
                    var currentValue = await session.ReadValueAsync(nodeId, cancellationToken);
                    writeValue = currentValue.Value != null
                        ? Convert.ChangeType(writeValueStr, currentValue.Value.GetType())
                        : writeValueStr;
                }
                else
                {
                    writeValue = OpcUaHelper.ConvertValue(writeValueStr, item.DataType);
                }

                writeValues.Add(new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(writeValue))
                });
            }

            var response = await session.WriteAsync(null, writeValues, cancellationToken);

            // 检查结果
            var failedCount = 0;
            for (int i = 0; i < response.Results.Count; i++)
            {
                if (StatusCode.IsBad(response.Results[i]))
                    failedCount++;
            }

            if (failedCount > 0)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"批量写入部分失败: {failedCount}/{setting.Items.Count} 个节点写入失败" }
                    }
                };
            }

            context.LogAction?.Invoke($"OPC UA 批量写入: {setting.Items.Count} 个节点完成");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已写入 {setting.Items.Count} 个节点" }
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
                    Error = new ErrorInfo { Message = $"OPC UA 批量写入失败: {ex.Message}" }
                }
            };
        }
    }
}
