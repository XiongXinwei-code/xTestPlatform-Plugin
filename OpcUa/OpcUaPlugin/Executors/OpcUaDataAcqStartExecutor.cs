using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 数据采集启动执行器</summary>
public sealed class OpcUaDataAcqStartExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaDataAcqStartPlugin().CreateSerializer();
        var setting = (OpcUaDataAcqStartSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var taskName = await Evaluator.EvalStringAsync(setting.TaskName, context);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var sessionKey = OpcUaHelper.GetSessionKey(connName);
            var taskKey = $"OpcUaDataAcq_{taskName}";

            // 获取 OPC UA 会话
            if (!context.Resources.TryGet<Session>(sessionKey, out var session))
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
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "采集节点列表为空" }
                    }
                };
            }

            // 解析节点表达式
            var resolvedItems = new List<OpcUaDataAcqItem>();
            foreach (var item in setting.Items)
            {
                var nodeId = await Evaluator.EvalStringAsync(item.NodeId, context);
                resolvedItems.Add(new OpcUaDataAcqItem { NodeId = nodeId, ColumnName = item.ColumnName });
            }

            // 启动后台采集任务（有界 FIFO 缓冲）
            var acqTask = new OpcUaDataAcqTask(taskName, session, resolvedItems, setting.SamplingIntervalMs, setting.MaxDurationMs, setting.BufferSize);
            // Set 会自动销毁同名旧任务（如上次运行异常终止未停止）
            context.Resources.Set(taskKey, acqTask);
            // 同时保存 items 配置供 Stop 步骤使用
            context.Resources.Set(taskKey + "_items", resolvedItems);

            context.LogAction?.Invoke($"OPC UA 数据采集已启动: {taskName} ({resolvedItems.Count} 节点, {setting.SamplingIntervalMs}ms 间隔)");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"采集已启动: {taskName}" }
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
                    Error = new ErrorInfo { Message = $"启动数据采集失败: {ex.Message}" }
                }
            };
        }
    }
}
