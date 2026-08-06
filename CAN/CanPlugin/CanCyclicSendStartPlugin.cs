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
        "启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧，直到执行 CAN_Cyclic_SendStop 停止。" +
        "用于模拟整车网络环境（如车速、转速等信号）。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的CAN连接名), TaskName(string,表达式,任务标识名,Stop时用此名称停止), " +
        "EnableLog(bool,是否输出发送日志,默认false), Messages(集合,周期报文列表,每个元素结构见下方JSON示例)。" +
        "Messages 元素JSON示例: {\"CanId\":\"\\\"0x185\\\"\",\"FrameType\":\"Standard\",\"Data\":\"\\\"FF FF FF FF FF FF FF FF\\\"\",\"CycleTimeMs\":100,\"Enabled\":true} " +
        "FrameType可选值: Standard, Extended。CanId和Data为表达式字段，字面量值需用引号包裹如\"\\\"0x185\\\"\"。";

    public override IStepExecutor CreateExecutor() => new CanCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var enabledCount = s.Messages.Count(m => m.Enabled);
        return $"CyclicSendStart {s.ConnectionName} Task={s.TaskName} ({enabledCount} messages)";
    }
}
