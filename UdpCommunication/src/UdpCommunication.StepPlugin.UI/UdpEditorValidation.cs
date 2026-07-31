using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using UdpCommunication.StepPlugin.Transport;
using UdpCommunication.StepPlugin.Validation;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace UdpCommunication.StepPlugin.UI;

internal static class UdpEditorValidation
{
    public static UdpSendSetting Deserialize(byte[] data, IStepSettingSerializer serializer, bool receive) =>
        data is { Length: > 0 }
            ? (UdpSendSetting)serializer.Deserialize(data, 1)
            : (UdpSendSetting)serializer.CreateDefault();

    public static IReadOnlyList<StepSettingError> Validate(UdpSendSetting setting, bool receive, IExecutionContext context)
    {
        var errors = new List<StepSettingError>();
        if (UdpSettingsValidator.ValidateEndpoint(new UdpEndpointOptions(setting.LocalAddress, setting.LocalPort, setting.RemoteAddress, setting.RemotePort)) is not null) errors.Add(StepSettingError.Error("UDP_001", "IP 地址或端口配置无效"));
        try { UdpMessageCodec.Encode(setting.RequestData, setting.RequestFormat); } catch (FormatException) { errors.Add(StepSettingError.Error("UDP_003", "发送十六进制报文格式无效")); }
        if (receive && setting is UdpSendAndReceiveSetting s) { if (s.ReceiveTimeoutMs <= 0) errors.Add(StepSettingError.Error("UDP_004", "接收超时必须大于 0")); try { UdpMessageCodec.Encode(s.ExpectedReply, s.ReplyFormat); } catch (FormatException) { errors.Add(StepSettingError.Error("UDP_005", "期望回复十六进制格式无效")); } if (!string.IsNullOrWhiteSpace(s.ResponseVariable) && !context.HasVariable(s.ResponseVariable)) errors.Add(StepSettingError.Warning("UDP_W01", "回复变量未定义，运行时将尝试写入该变量")); }
        return errors;
    }
}
