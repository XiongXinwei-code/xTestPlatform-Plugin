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

        SemaphoreSlim? registrationGate = null;
        bool ownsGate = false;
        var budget = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Http");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                execution.ThrowIfExpired();
            }
            var step = context.CurrentStep!.Step;
            var serializer = new HttpClientClosePlugin().CreateSerializer();
            var setting = (HttpClientCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            var clientName = await Evaluator.EvalStringAsync(setting.ClientName, context);
            CheckExecution();
            if (string.IsNullOrWhiteSpace(clientName))
                return Error("客户端标识名未配置");

            var key = HttpHelper.GetClientKey(clientName);
            registrationGate = HttpHelper.GetRegistryGate(context.Resources);
            if (!await registrationGate.WaitAsync(setting.TimeoutMs, cancellationToken))
                throw new TimeoutException($"等待 HTTP 客户端关闭生命周期操作超过 {setting.TimeoutMs} ms");
            ownsGate = true;
            CheckExecution();
            if (!context.Resources.Contains(key))
            {
                if (setting.IgnoreIfNotFound)
                {
                    context.Log(LogLevel.Warn, $"HTTP 客户端不存在，已忽略关闭操作: {clientName}");
                    cancellationToken.ThrowIfCancellationRequested();
                    CheckExecution();
                    return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed } };
                }

                return Error($"未找到 HTTP 客户端: {clientName}");
            }

            if (context.Resources.TryGet<HttpClientResource>(key, out var owner))
            {
                int remaining = setting.TimeoutMs - (int)Math.Min(int.MaxValue, budget.ElapsedMilliseconds);
                if (remaining <= 0) throw new TimeoutException($"HTTP 客户端关闭总超时 {setting.TimeoutMs} ms 已到");
                await owner.CloseAsync(remaining, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                CheckExecution();
            }
            context.Resources.Remove(key);
            context.Log($"HTTP 客户端已释放: {clientName}");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = clientName }
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
        finally { if (ownsGate) registrationGate!.Release(); }
    }

    private static ExecutionResult Error(string message, Exception? ex = null)
        => ex is null ? ExecutionResults.Error(message) : ExecutionResults.FromException(ex, message);
}
