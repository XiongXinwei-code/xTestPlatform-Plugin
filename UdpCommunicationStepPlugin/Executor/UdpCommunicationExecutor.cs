using System.Net;
using System.Net.Sockets;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using UdpCommunicationStepPlugin.Infrastructure;
using UdpCommunicationStepPlugin.Setting;

namespace UdpCommunicationStepPlugin.Executor;

public sealed class UdpCommunicationExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep!.Step;
            var serializer = new UdpCommunicationPlugin().CreateSerializer();
            var setting = (UdpCommunicationSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var issues = UdpSettingValidator.Validate(setting);
            if (issues.Count > 0) return Error(issues[0].Message);

            var endpoint = await ResolveEndpointAsync(setting.RemoteHost, setting.RemotePort, cancellationToken);
            var payload = UdpPayloadCodec.Encode(setting.Payload, setting.DataFormat);
            context.LogAction?.Invoke($"UDP 发送 {payload.Length} 字节至 {endpoint}。");

            if (setting.OperationMode == UdpOperationMode.SendOnly)
            {
                await UdpTransport.SendAsync(endpoint, setting.LocalPort, payload, cancellationToken);
                return Passed($"Sent {payload.Length} bytes");
            }

            var responseBytes = await UdpTransport.SendAndReceiveAsync(endpoint, setting.LocalPort, payload, TimeSpan.FromMilliseconds(setting.ResponseTimeoutMs), cancellationToken);
            var response = UdpPayloadCodec.Decode(responseBytes, setting.DataFormat);
            context.LogAction?.Invoke($"UDP 收到响应: {response}");
            if (!string.IsNullOrWhiteSpace(setting.ResponseVariableName)) context.SetVariable($"Step.{setting.ResponseVariableName}", response);
            var matched = UdpResponseMatcher.IsMatch(response, setting.ExpectedResponse, setting.ResponseMatchMode);
            return new ExecutionResult { StepResult = new StepResult { Status = matched ? TestStatus.Passed : TestStatus.Failed, Value = response, UpperBound = setting.ExpectedResponse, Condition = setting.ResponseMatchMode.ToString() } };
        }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception exception) { return Error(exception.Message); }
    }

    private static async Task<IPEndPoint> ResolveEndpointAsync(string host, int port, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var address = addresses.FirstOrDefault(item => item.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
            ?? throw new SocketException((int)SocketError.HostNotFound);
        return new IPEndPoint(address, port);
    }

    private static ExecutionResult Passed(string value) => new() { StepResult = new StepResult { Status = TestStatus.Passed, Value = value } };
    private static ExecutionResult Error(string message) => new() { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = message } } };
}
