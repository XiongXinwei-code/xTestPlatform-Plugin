using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAiStartPlugin : StepPluginBase<NiDaqAiStartSetting>
{
    public override string StepTypeId => "NiDaq.AiStart";
    public override string DisplayName => "NiDaq_AI_Start";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "启动 NI DAQ AI 连续后台采集任务，边采边写磁盘（TDMS/CSV），内存中仅保留统计累加器。" +
        "Setting 字段：TaskName(string,表达式,任务标识), Channels(列表), SampleRate(double), " +
        "MaxDurationMs(int,0为无限), ExportFormat(枚举), OutputDirectory(string,表达式), " +
        "StatVariablePrefix(string,表达式), ReadBatchSize(int)。";

    public override IStepExecutor CreateExecutor() => new NiDaqAiStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Start: {s.TaskName} ({s.Channels.Count} ch @ {s.SampleRate}Hz)";
    }
}
