using System.Globalization;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanCrcCalculateExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? deadline = null;
        IDisposable? operation = null;
        int timeoutMs = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("离线 CRC 计算缺少当前步骤");
            var setting = (CanCrcCalculateSetting)new CanCrcCalculatePlugin().CreateSerializer()
                .Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            timeoutMs = setting.TimeoutMs;
            if (timeoutMs <= 0) throw new ArgumentException("离线 CRC 计算超时必须大于 0 ms");
            deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeoutMs);
            var token = deadline.Token;
            var guard = new CanExecutionGuard(context, token);
            operation = ExecutionScopedExecutor.OwnOperation(() =>
            {
                try { _ = deadline.CancelAsync(); }
                catch (ObjectDisposedException) { /* The completed operation has already disposed its deadline. */ }
            });
            void Check() { token.ThrowIfCancellationRequested(); guard.Check(); }
            CheckResultVariable(context, setting.ResultVariable);
            Check();
            var evaluation = Task.Run(async () =>
            {
                Check();
                string value = await Evaluator.EvalStringAsync(setting.Data, context).ConfigureAwait(false);
                Check();
                return value;
            }, CancellationToken.None);
            string hex;
            try { hex = await evaluation.WaitAsync(token).ConfigureAwait(false); }
            catch
            {
                _ = evaluation.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                throw;
            }
            Check();
            var protection = await CanPayloadProtectionExpressions.ResolveAsync(setting.Protection, context, token, Check, guard.Log).ConfigureAwait(false);
            Check();
            byte[] payload = CanHelper.ParseHexData(hex);
            ulong crc = CanCrcFunctions.Calculate(payload, protection);
            string result = "0x" + crc.ToString($"X{CanPayloadProtection.GetParameters(protection).Width / 4}", CultureInfo.InvariantCulture);
            Check();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
            {
                CheckResultVariable(context, setting.ResultVariable);
                guard.SetVariable(setting.ResultVariable, result);
            }
            Check();
            return new() { StepResult = new() { Status = TestStatus.Passed, Value = result } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && deadline?.IsCancellationRequested == true)
                ex = new TimeoutException($"离线 CRC 计算超过配置总期限 {timeoutMs} ms", ex);
            return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"离线 CRC 计算失败：{ex.Message}") } };
        }
        finally { operation?.Dispose(); deadline?.Dispose(); }
    }

    private static void CheckResultVariable(IExecutionContext context, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (path.IndexOf('.') <= 0 || !context.HasVariable(path) || context.GetVariable(path) is { } value && value is not string)
            throw new ArgumentException("CRC 结果变量必须为已声明的字符串变量完整路径，或留空");
    }
}
