using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 断开连接执行器</summary>
public sealed class OpcUaDisconnectExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new OpcUaDisconnectPlugin().CreateSerializer();
        var setting = (OpcUaDisconnectSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = OpcUaHelper.GetSessionKey(connName);

            if (context.Resources.TryGet<Session>(key, out var session))
            {
                await session.CloseAsync(cancellationToken);
                context.Resources.Remove(key);
                context.LogAction?.Invoke($"OPC UA 连接已断开: {connName}");
            }
            else
            {
                context.LogAction?.Invoke($"OPC UA 连接 {connName} 不存在或已断开");
            }

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = $"已断开: {connName}" }
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
                    Error = ErrorInfo.FromException(ex, $"OPC UA 断开失败: {ex.Message}")
                }
            };
        }
    }
}
