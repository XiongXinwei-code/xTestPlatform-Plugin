using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAiReadPlugin : StepPluginBase<NiDaqAiReadSetting>
{
    public override string StepTypeId => "NiDaq.AiRead";
    public override string DisplayName => "NiDaq_AI_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "从已启动的 AI 采集任务中读取数据，计算统计值并存入变量。" +
        "Setting 字段：TaskName(string,表达式), SamplesToRead(int), ResultVariablePrefix(string,表达式), ExportFormat(enum), OutputDirectory(string,表达式)。";

    public override IStepExecutor CreateExecutor() => new NiDaqAiReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Read: {s.TaskName} → {s.ResultVariablePrefix}";
    }
}
