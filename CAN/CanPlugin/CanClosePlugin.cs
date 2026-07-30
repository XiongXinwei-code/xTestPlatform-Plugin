using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanClosePlugin : StepPluginBase<CanCloseSetting>
{
    public override string StepTypeId => "IO.CanClose";
    public override string DisplayName => "CAN_Close";
    public override string Category => "CAN";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "关闭已打开的 CAN 通道并释放资源。" +
        "Setting 字段：ConnectionName(string,要关闭的连接标识名)。";

    public override IStepExecutor CreateExecutor() => new CanCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
