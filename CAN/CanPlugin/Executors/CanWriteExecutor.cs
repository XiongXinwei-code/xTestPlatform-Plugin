using System.Diagnostics;
using CAN.Helpers;
using CAN.Models;
using CAN.Runtime;
using PluginCommon;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? timeout = null;
        IDisposable? operation = null;
        int timeoutMs = 0;
        try
        {
            var guard = new CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var setting = (CanWriteSetting)new CanWritePlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            timeoutMs = setting.TimeoutMs;
            if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs));
            long started = Stopwatch.GetTimestamp();
            timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs); var token = timeout.Token;
            operation = ExecutionScopedExecutor.OwnOperation(() => { try { _ = timeout.CancelAsync(); } catch (ObjectDisposedException) { /* Already disarmed and disposed. */ } });
            void Check() { guard.Check(); token.ThrowIfCancellationRequested(); }
            async Task<string> Evaluate(string expression)
            {
                Check(); var result = await Evaluator.EvalStringAsync(expression, context).WaitAsync(token).ConfigureAwait(false); Check(); return result;
            }
            int Remaining()
            {
                Check(); int remaining = timeoutMs - (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                if (remaining <= 0) throw new TimeoutException($"CAN 发送超过配置总期限 {timeoutMs} ms");
                return remaining;
            }
            var connName = await Evaluate(setting.ConnectionName).ConfigureAwait(false);
            var key = CanHelper.GetAdapterKey(connName);
            bool found = context.Resources.TryGet<CanConnectionResource>(key, out var connection);
            // Resource lookup can overlap cancellation/retirement. Recheck before
            // reporting a missing resource so caller cancellation remains Aborted.
            Check();
            if (!found || connection == null)
                throw new InvalidOperationException($"CAN 连接未找到或不支持可确认退出的生命周期：{connName}，请执行 CAN_Open");
            guard.Watch(key, connection); Check();
            var requests = new List<ProtectedCanFrame>();
            async Task Add(string id, string data, CanFrameType type, bool fd, CanPayloadProtectionSetting protection, string sender, int row)
            {
                try
                {
                    protection = await CanPayloadProtectionExpressions.ResolveAsync(protection, context, token, Check, guard.Log).ConfigureAwait(false);
                    var frame = new CanMessage { Id = CanHelper.ParseCanId(await Evaluate(id).ConfigureAwait(false)),
                        Data = CanHelper.ParseHexData(await Evaluate(data).ConfigureAwait(false)), FrameType = type, IsFd = fd };
                    CanHelper.ValidateTransmitFrame(frame); CanPayloadProtection.Validate(protection, frame.Data.Length);
                    requests.Add(new(frame, protection, sender, row));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new InvalidOperationException($"CAN 原列表第 {row} 行求值或配置失败，整批未发送：{ex.Message}", ex); }
            }
            if (setting.UseMessageList)
            {
                if (setting.Messages == null || setting.Messages.Count is < 1 or > 128) throw new ArgumentException("CAN 多报文列表需要 1～128 行");
                for (int i = 0; i < setting.Messages.Count; i++)
                {
                    var row = setting.Messages[i] ?? throw new ArgumentException($"CAN 列表第 {i + 1} 行为空");
                    if (!row.Enabled) continue;
                    if (row.Protection.RcEnabled && string.IsNullOrWhiteSpace(step.StepSetting.StepAddress))
                        throw new ArgumentException("启用 RC 的发送缺少稳定步骤地址");
                    await Add(row.CanId, row.Data, row.FrameType, row.UseFdFrame, row.Protection,
                        $"{step.StepSetting.StepAddress}/batch/{i}", i + 1).ConfigureAwait(false);
                }
                if (requests.Count == 0) throw new ArgumentException("CAN 多报文列表没有启用的报文");
            }
            else await Add(setting.CanId, setting.Data, setting.FrameType, setting.UseFdFrame, setting.Protection, step.StepSetting.StepAddress, 1).ConfigureAwait(false);
            var submitted = setting.UseMessageList
                ? await guard.RunAsync(() => connection.WriteProtectedBatchAsync(requests.ToArray(), Remaining(), token)).ConfigureAwait(false)
                : new[] { await guard.RunAsync(() => connection.WriteProtectedAsync(requests[0].Frame, requests[0].Protection,
                    requests[0].SenderKey, Remaining(), token)).ConfigureAwait(false) };
            Check();
            string value = string.Join("; ", submitted.Select(frame => $"0x{frame.Id:X}: {BitConverter.ToString(frame.Data).Replace("-", " ")}"));
            guard.Log($"CAN 驱动提交完成（非上总线确认），共 {submitted.Length} 帧：{value}");
            return new() { StepResult = new() { Status = TestStatus.Passed, Value = value } };
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted, Value = ex.Message } }; }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && timeout?.IsCancellationRequested == true)
                ex = new TimeoutException($"CAN 发送超过配置总期限 {timeoutMs} ms；{ex.Message}", ex);
            return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"CAN 发送失败: {ex.Message}") } };
        }
        finally { operation?.Dispose(); timeout?.Dispose(); }
    }
}
