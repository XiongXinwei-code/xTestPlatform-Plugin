using UdpCommunication.Helpers;
using UdpCommunication.Models;
using UdpCommunication.Transport;
using UdpCommunication.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.Executors;

public sealed class UdpOpenExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new UdpOpenPlugin().CreateSerializer();
            var s = (UdpOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            var localAddress = await Evaluator.EvalStringAsync(s.LocalAddress, context);

            var validation = UdpSettingsValidator.ValidateLocalEndpoint(localAddress, s.LocalPort);
            if (validation is not null)
            {
                context.LogAction?.Invoke($"UDP 配置错误：{validation}");
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = validation }
                    }
                };
            }

            var transport = new UdpTransport(localAddress, s.LocalPort);

            // 使用 *当前步骤的 StepAddress* 作为 RuntimeData key，使后续 Send/Receive/Close 步骤
            // 能够通过选中同一个 Open 步骤的地址来引用此 transport。
            var currentStepAddress = context.CurrentStep.StepAddress ?? string.Empty;
            var key = UdpHelper.GetConnectionKey(currentStepAddress);

            if (!string.IsNullOrEmpty(currentStepAddress)
                && context.CurrentStep.RuntimeData.TryGetValue(key, out var existing)
                && existing is IUdpTransport oldTransport)
            {
                try { oldTransport.Dispose(); } catch { /* 忽略销毁异常 */ }
                context.LogAction?.Invoke($"UDP {key} 检测到已有连接，已自动关闭旧连接");
            }

            context.CurrentStep.RuntimeData[key] = transport;
            var actualLocalPort = transport.LocalEndPoint.Port;
            context.LogAction?.Invoke($"UDP 已打开：{transport.LocalEndPoint} (步骤地址 {currentStepAddress})");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"{localAddress}:{actualLocalPort}"
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            context.LogAction?.Invoke($"UDP 打开失败：{ex.Message}");
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = ex.Message }
                }
            };
        }
    }
}
