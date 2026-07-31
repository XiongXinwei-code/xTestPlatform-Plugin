using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStopPlugin : StepPluginBase<CanCyclicSendStopSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStop";
    public override string DisplayName => "CAN_Cyclic_SendStop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "停止指定的 CAN 周期发送任务。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), TaskName(string,表达式,要停止的任务标识名)。";

    public override IStepExecutor CreateExecutor() => new CanCyclicSendStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop {s.ConnectionName} Task={s.TaskName}";
    }
}
