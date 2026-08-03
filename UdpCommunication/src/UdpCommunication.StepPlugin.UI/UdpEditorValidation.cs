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
    public static IReadOnlyList<StepSettingError> ValidateSafely(
        byte[] data,
        IStepSettingSerializer serializer,
        bool receive,
        IExecutionContext context)
    {
        try
        {
            var setting = data is { Length: > 0 }
                ? (UdpSendSetting)serializer.Deserialize(data, serializer.SettingVersion)
                : (UdpSendSetting)serializer.CreateDefault();
            return Validate(setting, receive, context);
        }
        catch (Exception ex)
        {
            return [StepSettingError.Error("UDP_000", $"UDP 配置无法读取：{ex.Message}")];
        }
    }

    public static IReadOnlyList<StepSettingError> Validate(UdpSendSetting setting, bool receive, IExecutionContext context)
    {
        var errors = new List<StepSettingError>();
        if (UdpSettingsValidator.ValidateEndpoint(new UdpEndpointOptions(setting.LocalAddress, setting.LocalPort, setting.RemoteAddress, setting.RemotePort)) is not null) errors.Add(StepSettingError.Error("UDP_001", "IP 地址或端口配置无效"));
        try { UdpMessageCodec.Encode(setting.RequestData, setting.RequestFormat); } catch (Exception) { errors.Add(StepSettingError.Error("UDP_003", "发送报文格式无效")); }
        if (receive && setting is not UdpSendAndReceiveSetting) errors.Add(StepSettingError.Error("UDP_000", "UDP 收发步骤配置类型无效"));
        if (receive && setting is UdpSendAndReceiveSetting s)
        {
            if (s.ReceiveTimeoutMs <= 0)
            {
                errors.Add(StepSettingError.Error("UDP_004", "接收超时必须大于 0"));
            }

            try
            {
                UdpMessageCodec.Encode(s.ExpectedReply, s.ReplyFormat);
            }
            catch (Exception)
            {
                errors.Add(StepSettingError.Error("UDP_005", "期望回复报文格式无效"));
            }

            var responseVariableError = UdpResponseVariable.Validate(s.ResponseVariable, context);
            if (responseVariableError is not null)
            {
                errors.Add(StepSettingError.Error("UDP_006", responseVariableError));
            }
        }
        return errors;
    }
}
