using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using UdpCommunication.StepPlugin.Transport;
using UdpCommunication.StepPlugin.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin.Executors;

public sealed class UdpSendExecutor(IStepSettingSerializer serializer) : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("未找到当前步骤配置");
            var setting = (UdpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var endpoint = new UdpEndpointOptions(setting.LocalAddress, setting.LocalPort, setting.RemoteAddress, setting.RemotePort);
            var error = UdpSettingsValidator.ValidateEndpoint(endpoint);
            if (error is not null) return Failure(error);
            await new UdpTransport().SendAsync(endpoint, UdpMessageCodec.Encode(setting.RequestData, setting.RequestFormat), cancellationToken);
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = setting.RequestData } };
        }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } }; }
    }
    private static ExecutionResult Failure(string message) => new() { StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = message } } };
}
