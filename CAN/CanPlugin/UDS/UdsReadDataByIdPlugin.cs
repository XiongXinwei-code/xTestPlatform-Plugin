using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsReadDataByIdPlugin : StepPluginBase<UdsReadDataByIdSetting>
{
    public override string StepTypeId => "UDS.ReadDataByID";
    public override string DisplayName => "UDS_ReadDataByID";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "通过 DID 读取 ECU 数据（UDS 服务 0x22）。" +
        "Setting 字段：Did(string,表达式,数据标识符如0xF190), ResultVariable(string,结果存储变量路径), " +
        "ConnectionName(string,表达式,CAN连接名), TxId(string,表达式), RxId(string,表达式), ResponseTimeoutMs(int)。";

    public override IStepExecutor CreateExecutor() => new UdsReadDataByIdExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"ReadDataByID DID={s.Did} → {s.ResultVariable}";
    }
}
