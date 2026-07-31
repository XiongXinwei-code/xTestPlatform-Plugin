using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStartPlugin : StepPluginBase<CanCyclicSendStartSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStart";
    public override string DisplayName => "CAN_Cyclic_SendStart";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧。" +
        "用于模拟整车网络环境（如车速、转速等信号）。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), TaskName(string,表达式,任务标识名), " +
        "EnableLog(bool,是否输出发送日志), Messages(List<CyclicMessageItem>,报文列表)。";

    public override IStepExecutor CreateExecutor() => new CanCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var enabledCount = s.Messages.Count(m => m.Enabled);
        return $"CyclicSendStart {s.ConnectionName} Task={s.TaskName} ({enabledCount} messages)";
    }
}
