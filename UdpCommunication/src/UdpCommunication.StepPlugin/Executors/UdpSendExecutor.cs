using System.Diagnostics;
using UdpCommunication.StepPlugin.Display;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using UdpCommunication.StepPlugin.Transport;
using UdpCommunication.StepPlugin.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin.Executors;

public sealed class UdpSendExecutor : IStepExecutor
{
    private readonly IStepSettingSerializer _serializer;
    private readonly IUdpTransport _transport;

    public UdpSendExecutor(IStepSettingSerializer serializer, IUdpTransport? transport = null)
    {
        _serializer = serializer;
        _transport = transport ?? new UdpTransport();
    }

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var step = context.CurrentStep?.Step ?? throw new InvalidOperationException("未找到当前步骤配置");
            var setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (UdpSendSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion)
                : (UdpSendSetting)_serializer.CreateDefault();
            var endpoint = new UdpEndpointOptions(setting.LocalAddress, setting.LocalPort, setting.RemoteAddress, setting.RemotePort);
            var error = UdpSettingsValidator.ValidateEndpoint(endpoint);
            if (error is not null)
            {
                return ConfigurationError(context, error);
            }

            var payload = UdpMessageCodec.Encode(setting.RequestData, setting.RequestFormat);
            Log(
                context,
                $"UDP 发送开始：{endpoint.LocalAddress}:{endpoint.LocalPort} → " +
                $"{endpoint.RemoteAddress}:{endpoint.RemotePort}，格式 {setting.RequestFormat}，" +
                $"{payload.Length} 字节，内容 {UdpDescriptionFormatter.Preview(setting.RequestData)}");
            await _transport.SendAsync(endpoint, payload, cancellationToken);
            Log(context, $"UDP 发送完成：已发送 {payload.Length} 字节");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = setting.RequestData } };
        }
        catch (OperationCanceledException)
        {
            Log(context, "UDP 发送已取消");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            Log(context, $"UDP 发送失败：{ex.Message}");
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } };
        }
    }

    private static ExecutionResult ConfigurationError(IExecutionContext context, string message)
    {
        Log(context, $"UDP 配置错误：{message}");
        return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = message } } };
    }

    private static void Log(IExecutionContext context, string message)
    {
        try
        {
            context.LogAction?.Invoke(message);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"UDP 平台日志输出失败：{ex.Message}");
        }
    }
}
