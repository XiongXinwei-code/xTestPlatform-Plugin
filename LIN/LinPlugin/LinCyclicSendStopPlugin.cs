using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinCyclicSendStopPlugin : StepPluginBase<LinCyclicSendStopSetting>
{
    public override string StepTypeId   => "IO.LinCyclicSendStop";
    public override string DisplayName  => "LIN_Cyclic_SendStop";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "停止指定名称的 LIN 周期发送任务。" +
        "Setting 字段：TaskName(string,表达式,要停止的任务标识名,默认\"LinCyclicTask1\")。";

    public override IStepExecutor CreateExecutor() => new LinCyclicSendStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop TaskName={s.TaskName}";
    }
}
