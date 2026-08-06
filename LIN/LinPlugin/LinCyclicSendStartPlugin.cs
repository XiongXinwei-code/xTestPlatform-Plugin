using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinCyclicSendStartPlugin : StepPluginBase<LinCyclicSendStartSetting>
{
    public override string StepTypeId   => "IO.LinCyclicSendStart";
    public override string DisplayName  => "LIN_CyclicSendStart";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "启动 LIN 周期发送任务，在后台按各帧配置的周期持续发送多个 LIN 帧。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"LIN1\"), " +
        "TaskName(string,表达式,任务标识名用于Stop步骤引用,默认\"LinCyclicTask1\"), " +
        "EnableLog(bool,是否输出日志,默认false), " +
        "Frames(集合,周期发送帧列表,每个元素结构见下方JSON示例)。" +
        "Frames 元素JSON示例: {\"FrameId\":\"0\",\"Data\":\"\\\"FF FF FF FF FF FF FF FF\\\"\",\"CycleTimeMs\":100,\"ChecksumType\":\"Enhanced\",\"Enabled\":true}。" +
        "ChecksumType 可选值: Classic, Enhanced。";

    public override IStepExecutor CreateExecutor() => new LinCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStart TaskName={s.TaskName}, 帧数={s.Frames.Count}";
    }
}
