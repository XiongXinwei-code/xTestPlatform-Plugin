using UdpCommunicationStepPlugin.Infrastructure;

namespace UdpCommunicationStepPlugin.Setting;

public sealed record UdpValidationIssue(string Code, string Message);

public static class UdpSettingValidator
{
    public static IReadOnlyList<UdpValidationIssue> Validate(UdpCommunicationSetting setting)
    {
        var issues = new List<UdpValidationIssue>();
        if (string.IsNullOrWhiteSpace(setting.RemoteHost)) issues.Add(new("UDP_001", "远程主机不能为空。"));
        if (setting.RemotePort is < 1 or > 65535) issues.Add(new("UDP_002", "远程端口必须在 1 到 65535 之间。"));
        if (setting.LocalPort is < 0 or > 65535) issues.Add(new("UDP_003", "本地端口必须在 0 到 65535 之间。"));
        if (setting.OperationMode == UdpOperationMode.SendAndWaitForResponse && setting.ResponseTimeoutMs <= 0) issues.Add(new("UDP_004", "响应超时必须大于 0。"));
        if (setting.OperationMode == UdpOperationMode.SendAndWaitForResponse && setting.ResponseMatchMode != UdpResponseMatchMode.AnyResponse && string.IsNullOrEmpty(setting.ExpectedResponse)) issues.Add(new("UDP_005", "响应匹配需要配置期望响应。"));
        try { UdpPayloadCodec.Encode(setting.Payload, setting.DataFormat); }
        catch (FormatException) { issues.Add(new("UDP_006", "十六进制发送数据格式无效。")); }
        return issues;
    }
}
