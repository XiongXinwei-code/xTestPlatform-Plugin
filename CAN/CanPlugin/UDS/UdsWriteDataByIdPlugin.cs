using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsWriteDataByIdPlugin : StepPluginBase<UdsWriteDataByIdSetting>
{
    public override string StepTypeId => "UDS.WriteDataByID";
    public override string DisplayName => "UDS_WriteDataByID";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "通过 DID 向 ECU 写入数据（UDS 服务 0x2E）。" +
        "Setting 字段：Did(string,表达式,数据标识符), Data(string,表达式,十六进制写入数据), " +
        "ConnectionName(string,表达式,CAN连接名), TxId(string,表达式), RxId(string,表达式), ResponseTimeoutMs(int)。";

    public override IStepExecutor CreateExecutor() => new UdsWriteDataByIdExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WriteDataByID DID={s.Did} Data=[{s.Data}]";
    }
}
