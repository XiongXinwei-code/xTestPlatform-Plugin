using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Http.Executors;

/// <summary>
/// 释放命名 HTTP 客户端
/// </summary>
public sealed class HttpClientCloseExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new HttpClientClosePlugin().CreateSerializer();
        var setting = (HttpClientCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clientName = await Evaluator.EvalStringAsync(setting.ClientName, context);
            if (string.IsNullOrWhiteSpace(clientName))
                return Error("客户端标识名未配置");

            var key = HttpHelper.GetClientKey(clientName);
            if (!context.Resources.Contains(key))
            {
                if (setting.IgnoreIfNotFound)
                {
                    context.LogAction?.Invoke($"HTTP 客户端不存在，已忽略关闭操作: {clientName}");
                    return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed } };
                }

                return Error($"未找到 HTTP 客户端: {clientName}");
            }

            context.Resources.Remove(key);
            context.LogAction?.Invoke($"HTTP 客户端已释放: {clientName}");

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Passed, Value = clientName }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return Error($"释放 HTTP 客户端失败: {ex.Message}", ex);
        }
    }

    private static ExecutionResult Error(string message, Exception? ex = null) => new()
    {
        StepResult = new StepResult
        {
            Status = TestStatus.Error,
            Error = ex is null ? new ErrorInfo { Message = message } : ErrorInfo.FromException(ex, message)
        }
    };
}
