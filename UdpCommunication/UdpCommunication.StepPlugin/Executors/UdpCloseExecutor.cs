using UdpCommunication.Helpers;
using UdpCommunication.Models;
using UdpCommunication.Transport;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.Executors;

public sealed class UdpCloseExecutor : IStepExecutor
{
    public Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new UdpClosePlugin().CreateSerializer();
            var s = (UdpCloseSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

            if (string.IsNullOrWhiteSpace(s.OpenStepAddress))
            {
                context.LogAction?.Invoke("UDP 关闭错误：未指定 OpenStepAddress");
                return Task.FromResult(new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "未指定 OpenStepAddress（请先选择一个 UDP_Open 步骤）" }
                    }
                });
            }

            var key = UdpHelper.GetConnectionKey(s.OpenStepAddress);

            if (context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) && obj is IUdpTransport transport)
            {
                transport.Dispose();
                context.CurrentStep.RuntimeData.Remove(key);
                context.LogAction?.Invoke($"UDP {key} 已关闭");
            }
            else
            {
                context.LogAction?.Invoke($"UDP {key} 未找到或已关闭");
            }

            return Task.FromResult(new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = s.OpenStepAddress
                }
            });
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } });
        }
        catch (Exception ex)
        {
            context.LogAction?.Invoke($"UDP 关闭失败：{ex.Message}");
            return Task.FromResult(new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = ex.Message }
                }
            });
        }
    }
}
