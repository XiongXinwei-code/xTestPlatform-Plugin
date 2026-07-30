using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsRawRequestPlugin : StepPluginBase<UdsRawRequestSetting>
{
    public override string StepTypeId => "UDS.RawRequest";
    public override string DisplayName => "UDS_RawRequest";
    public override string Category => "UDS";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "发送原始 UDS 请求数据（通用，任意服务）。" +
        "Setting 字段：RequestData(string,十六进制请求数据如'10 03'), " +
        "WaitResponse(bool,是否等待响应), ResultVariable(string,响应存储变量)。";

    public override IStepExecutor CreateExecutor() => new UdsRawRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RawRequest [{s.RequestData}] → {s.ResultVariable}";
    }
}
