using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinClosePlugin : StepPluginBase<LinCloseSetting>
{
    public override string StepTypeId   => "IO.LinClose";
    public override string DisplayName  => "LIN_Close";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "关闭 LIN 通道，释放硬件资源。" +
        "Setting 字段：ConnectionName(string,表达式,要关闭的连接标识名,默认\"LIN1\")。";

    public override IStepExecutor CreateExecutor() => new LinCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
