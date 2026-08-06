using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDiagRequestPlugin : StepPluginBase<DoipDiagRequestSetting>
{
    public override string StepTypeId  => "DoIP.DiagRequest";
    public override string DisplayName => "DoIP_DiagRequest";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "通过已建立的 DoIP 会话发送 UDS 诊断请求并接收响应，收到 UDS 负响应(0x7F)时步骤判定为 Failed。" +
        "Setting 字段：SessionName(string,表达式,会话标识名,默认\"DOIP1\"), " +
        "TargetAddress(string,表达式,ECU逻辑地址,默认\"0x1000\"), " +
        "RequestData(string,表达式,UDS请求十六进制数据,默认\"22 F1 90\"), " +
        "TimeoutMs(int,响应超时毫秒,默认3000), " +
        "ResultVariable(string,存储响应数据的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new DoipDiagRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP DiagRequest: {s.SessionName} -> {s.TargetAddress} [{s.RequestData}]";
    }
}
