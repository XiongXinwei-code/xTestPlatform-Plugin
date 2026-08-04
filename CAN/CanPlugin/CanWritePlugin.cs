using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanWritePlugin : StepPluginBase<CanWriteSetting>
{
    public override string StepTypeId => "IO.CanWrite";
    public override string DisplayName => "CAN_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "向已打开的 CAN 通道发送一帧报文。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的CAN连接名), CanId(string,表达式,CAN ID如0x7DF), " +
        "FrameType(枚举:Standard/Extended,默认Standard), Data(string,表达式,十六进制数据如'02 10 01'), " +
        "UseFdFrame(bool,是否使用CAN FD帧,默认false)。";

    public override IStepExecutor CreateExecutor() => new CanWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.ConnectionName} ID={s.CanId} [{s.Data}]";
    }
}
