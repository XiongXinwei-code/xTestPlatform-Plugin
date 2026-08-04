using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsRawRequestPlugin : StepPluginBase<UdsRawRequestSetting>
{
    public override string StepTypeId => "UDS.RawRequest";
    public override string DisplayName => "UDS_RawRequest";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "发送原始 UDS 请求数据（通用，任意服务）。适用于其他专用 UDS 插件未覆盖的服务。" +
        "Setting 字段：RequestData(string,表达式,十六进制请求数据如'10 03'), " +
        "WaitResponse(bool,是否等待响应,默认true), ResultVariable(string,结果变量名,写入类型:string 十六进制响应数据,可选), " +
        "ConnectionName(string,表达式,已打开的CAN连接名), TxId(string,表达式,请求CAN ID), RxId(string,表达式,响应CAN ID), ResponseTimeoutMs(int,响应超时,默认5000)。";

    public override IStepExecutor CreateExecutor() => new UdsRawRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RawRequest [{s.RequestData}] → {s.ResultVariable}";
    }
}
