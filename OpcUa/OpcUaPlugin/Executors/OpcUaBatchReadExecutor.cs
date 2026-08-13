using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 批量读取执行器</summary>
public sealed class OpcUaBatchReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaBatchReadPlugin().CreateSerializer();
        var setting = (OpcUaBatchReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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
                    StepResult = new StepResult { Status = TestStatus.Passed, Value = "无节点需要读取" }
                };
            }

            // 构建读取请求
            var nodesToRead = new ReadValueIdCollection();
            foreach (var item in setting.Items)
            {
                nodesToRead.Add(new ReadValueId
                {
                    NodeId = OpcUaHelper.ParseNodeId(item.NodeId),
                    AttributeId = Attributes.Value
                });
            }

            var response = await session.ReadAsync(null, 0, TimestampsToReturn.Both, nodesToRead, cancellationToken);

            // 将结果存入变量
            var results = new List<string>();
            for (int i = 0; i < setting.Items.Count; i++)
            {
                var dataValue = response.Results[i];
                var value = dataValue.Value?.ToString() ?? "";
                results.Add(value);

                if (!string.IsNullOrWhiteSpace(setting.Items[i].ResultVariable))
                {
                    context.SetVariable(setting.Items[i].ResultVariable, dataValue.Value);
                }
            }

            context.LogAction?.Invoke($"OPC UA 批量读取: {setting.Items.Count} 个节点完成");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = string.Join(", ", results) }
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
                    Error = new ErrorInfo { Message = $"OPC UA 批量读取失败: {ex.Message}" }
                }
            };
        }
    }
}
