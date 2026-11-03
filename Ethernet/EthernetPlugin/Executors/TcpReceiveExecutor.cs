using Ethernet.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class TcpReceiveExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        int timeoutMs = 0;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Ethernet");
            void CheckExecution()
            {
                cancellationToken.ThrowIfCancellationRequested();
                execution.ThrowIfExpired();
            }
            var step = context.CurrentStep!.Step;
            var serializer = new TcpReceivePlugin().CreateSerializer();
            var setting = (TcpReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            EthernetStringOutput.Validate(context, setting.ResultVariable, CheckExecution);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            ArgumentOutOfRangeException.ThrowIfNegative(setting.ExpectedLength);
            if (setting.MaxBytes is < 1 or > 16777216) throw new ArgumentException("MaxBytes 必须在 1~16777216 内");
            if (!setting.UseDelimiter && setting.ExpectedLength > setting.MaxBytes) throw new ArgumentException("ExpectedLength 不能超过 MaxBytes");
            if (!Enum.IsDefined(setting.Encoding)) throw new InvalidOperationException("TCP 接收编码无效");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            timeoutMs = setting.TimeoutMs;
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context);
            CheckExecution();

            byte[]? delimiter = null;
            if (setting.UseDelimiter)
            {
                var text = await EthernetExecutorHelper.EvalStringAsync(setting.DelimiterHex, context); CheckExecution();
                delimiter = EthernetDataHelper.Encode(text!, EthernetDataEncoding.Hex);
                if (delimiter.Length is < 1 or > 64 || delimiter.Length > setting.MaxBytes)
                    throw new ArgumentException("DelimiterHex 必须为 1~64 字节且不超过 MaxBytes");
            }
            var client = TcpConnectionManager.Get(name!, context.Resources);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(setting.TimeoutMs);

            var buffer = await TcpReceiveBuffer.For(client).ReadAsync(client, setting.ExpectedLength, delimiter,
                setting.IncludeDelimiter, setting.MaxBytes, cts.Token, CheckExecution);

            var result = EthernetDataHelper.Decode(buffer, setting.Encoding);

            context.Log($"TCP 接收: {name} 收到 {buffer.Length} 字节 [{EthernetDataHelper.Decode(buffer, EthernetDataEncoding.Hex)}]");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            if (!ReferenceEquals(client, TcpConnectionManager.Get(name!, context.Resources)))
                throw new InvalidOperationException("TCP 连接已被替换，拒绝发布旧代次响应");

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, result);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = result
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"TCP 接收超时({timeoutMs}ms)")
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
                    Error = ErrorInfo.FromException(ex, $"TCP 接收失败: {ex.Message}")
                }
            };
        }
    }
}
