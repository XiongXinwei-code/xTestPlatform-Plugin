using PluginCommon;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.Executors;

/// <summary>
/// VISA 批量写入执行器，按顺序发送多条 SCPI 命令
/// </summary>
public sealed class VisaBatchWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = ExecutionGuard.Capture(context.Resources, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new VisaBatchWritePlugin().CreateSerializer();
            var setting = (VisaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            epoch.ThrowIfExpired();
            var key = VisaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<VisaSessionResource>(key, out var resource))
            {
                epoch.ThrowIfExpired();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"未找到 VISA 会话: {connName}" }
                    }
                };
            }

            // Resolve a stable command table before entering the background transaction.
            var commands = new List<(string Command, int DelayMs)>();
            foreach (var item in setting.Items)
            {
                if (item.DelayMs < 0) throw new ArgumentOutOfRangeException(nameof(item.DelayMs), "命令间延时不能为负数，尚未发送本批任何命令");
                var command = await Evaluator.EvalStringAsync(item.Command, context);
                epoch.ThrowIfExpired();
                if (!string.IsNullOrWhiteSpace(command)) commands.Add((command, item.DelayMs));
            }
            if (setting.TimeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            long totalMs = commands.Sum(x => (long)Math.Max(1, resource.IoTimeoutMs) + x.DelayMs);
            var timeoutMs = setting.TimeoutMs > 0 ? setting.TimeoutMs : resource.IoTimeoutMs == Timeout.Infinite
                ? Timeout.Infinite : (int)Math.Min(int.MaxValue, Math.Max(1, totalMs));
            epoch.ThrowIfExpired();
            var sent = await resource.RunAsync(async (session, token) =>
            {
                int count = 0;
                foreach (var item in commands)
                {
                    epoch.ThrowIfExpired();
                    resource.ThrowIfSubmissionStopped();
                    VisaHelper.Write(session, item.Command, resource.Terminator, () => { epoch.ThrowIfExpired(); resource.ThrowIfSubmissionStopped(); });
                    count++;
                    if (item.DelayMs > 0)
                        await Task.Delay(item.DelayMs, token);
                }
                return count;
            }, timeoutMs, "VISA 批量写入", cancellationToken);
            context.Log($"VISA BatchWrite: {connName} 已确认原始调用退出，发送 {sent} 条命令");
            epoch.ThrowIfExpired();
            VisaResultPublisher.EnsureActive(context, key, resource, cancellationToken);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已发送 {sent} 条命令" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TimeoutException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA 批量写入超时: {ex.Message}")
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"VISA 批量写入失败: {ex.Message}")
                }
            };
        }
    }

    /// <summary>获取打开会话时保存的终止符，未找到时默认换行符</summary>
    private static string GetTerminator(IExecutionContext context, string connName) =>
        context.Resources.TryGet<string>(VisaHelper.GetTerminatorKey(connName), out var term) ? term : "\n";
}
