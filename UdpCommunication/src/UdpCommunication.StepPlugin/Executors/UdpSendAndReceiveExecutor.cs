using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using UdpCommunication.StepPlugin.Transport;
using UdpCommunication.StepPlugin.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin.Executors;

public sealed class UdpSendAndReceiveExecutor(IStepSettingSerializer serializer) : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("未找到当前步骤配置");
            var setting = (UdpSendAndReceiveSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var endpoint = new UdpEndpointOptions(setting.LocalAddress, setting.LocalPort, setting.RemoteAddress, setting.RemotePort);
            var validation = UdpSettingsValidator.ValidateEndpoint(endpoint);
            if (validation is not null || setting.ReceiveTimeoutMs <= 0) return Fail(validation ?? "接收超时必须大于 0");
            var reply = await new UdpTransport().SendAndReceiveAsync(endpoint, UdpMessageCodec.Encode(setting.RequestData, setting.RequestFormat), TimeSpan.FromMilliseconds(setting.ReceiveTimeoutMs), cancellationToken);
            var actual = UdpMessageCodec.Decode(reply.Payload, setting.ReplyFormat);
            if (!string.IsNullOrWhiteSpace(setting.ResponseVariable)) context.SetVariable(setting.ResponseVariable, actual);
            var matched = string.IsNullOrEmpty(setting.ExpectedReply) || UdpMessageCodec.IsMatch(actual, setting.ExpectedReply, setting.MatchMode);
            return new ExecutionResult { StepResult = new StepResult { Status = matched ? TestStatus.Passed : TestStatus.Failed, Value = actual, UpperBound = setting.ExpectedReply, Condition = setting.MatchMode == UdpReplyMatchMode.Exact ? "完全相等" : "包含指定字段" } };
        }
        catch (TimeoutException ex) { return Fail(ex.Message); }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } }; }
    }
    private static ExecutionResult Fail(string message) => new() { StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = message } } };
}
