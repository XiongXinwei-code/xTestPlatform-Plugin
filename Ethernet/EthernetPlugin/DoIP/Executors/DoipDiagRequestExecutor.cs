using Ethernet.DoIP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP.Executors;

public sealed class DoipDiagRequestExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new DoipDiagRequestPlugin().CreateSerializer();
        var setting = (DoipDiagRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        string? name = null;
        try
        {
            name = await EthernetExecutorHelper.EvalStringAsync(setting.SessionName, context);
            var targetStr = await EthernetExecutorHelper.EvalStringAsync(setting.TargetAddress, context);
            var dataStr = await EthernetExecutorHelper.EvalStringAsync(setting.RequestData, context);

            var client = DoipConnectionManager.Get(name);
            var targetAddress = DoipHelper.ParseAddress(targetStr);
            var requestData = DoipHelper.ParseHexData(dataStr);

            if (setting.EnableLog)
                context.LogAction?.Invoke(
                    $"DoIP 诊断请求: {name} -> 0x{targetAddress:X4} [{DoipHelper.ToHex(requestData)}]");

            var response = await client.DiagRequestAsync(targetAddress, requestData, cancellationToken);
            var responseHex = DoipHelper.ToHex(response);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"DoIP 诊断响应: [{responseHex}]");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, responseHex);

            // UDS 负响应 0x7F 判定为失败
            if (response.Length >= 3 && response[0] == 0x7F)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Value = responseHex,
                        Error = new ErrorInfo { Message = $"UDS 负响应: NRC=0x{response[2]:X2}" }
                    }
                };

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = responseHex
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (OperationCanceledException)
        {
            // 超时后迟到的响应会残留在 TCP 流中，关闭会话防止后续请求读到旧数据
            if (name != null)
                DoipConnectionManager.Close(name);
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = "DoIP 诊断请求超时: 未在超时时间内收到响应，会话已关闭，请重新执行 DoIP_Connect" }
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
