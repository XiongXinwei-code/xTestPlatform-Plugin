using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipDiagRequestExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {

        string? name = null;
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
            var serializer = new DoipDiagRequestPlugin().CreateSerializer();
            var setting = (DoipDiagRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "DoIP 请求总超时必须大于 0");
            cancellationToken.ThrowIfCancellationRequested();
            CheckExecution();
            name = await EthernetExecutorHelper.EvalStringAsync(setting.SessionName, context);
            CheckExecution();
            var targetStr = await EthernetExecutorHelper.EvalStringAsync(setting.TargetAddress, context);
            CheckExecution();
            var dataStr = await EthernetExecutorHelper.EvalStringAsync(setting.RequestData, context);
            CheckExecution();

            var client = DoipConnectionManager.Get(name, context.Resources);
            var targetAddress = DoipHelper.ParseAddress(targetStr);
            var requestData = DoipHelper.ParseHexData(dataStr);

            context.Log(
                $"DoIP 诊断请求: {name} -> 0x{targetAddress:X4} [{DoipHelper.ToHex(requestData)}]");

            CheckExecution();
            var response = await client.DiagRequestAsync(targetAddress, requestData, setting.TimeoutMs, cancellationToken);
            var responseHex = DoipHelper.ToHex(response);

            context.Log($"DoIP 诊断响应: [{responseHex}]");
            cancellationToken.ThrowIfCancellationRequested(); // 日志可能阻塞；取消后不向序列发布迟到结果。
            CheckExecution();
            if (!ReferenceEquals(client, DoipConnectionManager.Get(name!, context.Resources)))
                throw new InvalidOperationException("DoIP 会话已关闭或被替换，拒绝发布旧代次响应");

            CheckExecution();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, responseHex);

            // UDS 负响应 0x7F 返回执行错误
            if (response.Length >= 3 && response[0] == 0x7F)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Value = responseHex,
                        Error = new ErrorInfo { Message = $"UDS 负响应: NRC=0x{response[2]:X2}" }
                    }
                };

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = responseHex
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (TimeoutException ex)
        {
            // 排队未提交时不能声称连接已关闭；真实事务失败只影响捕获的原实例。
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"DoIP 诊断请求超时: {ex.Message}")
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
                    Error = ErrorInfo.FromException(ex, $"DoIP 诊断请求失败: {ex.Message}")
                }
            };
        }
    }
}
