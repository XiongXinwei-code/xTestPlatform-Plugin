using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqSyncStartPlugin : StepPluginBase<NiDaqSyncStartSetting>
{
    public override string StepTypeId => "NiDaq.SyncStart";
    public override string DisplayName => "NiDaq_Sync_Start";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "启动 NI DAQ 同步采集任务（AI + 编码器共享采样时钟），适用于 EPS 扭矩-角度曲线等多源同步场景。" +
        "Setting 字段：TaskName(string,表达式), AiChannels(列表), EncoderChannels(列表), " +
        "SampleRate(double), MaxDurationMs(int), UseTrigger(bool), TriggerSource(string), " +
        "TriggerEdge(枚举), ExportFormat(枚举), OutputDirectory(string,表达式), " +
        "StatVariablePrefix(string,表达式), ReadBatchSize(int)。";

    public override IStepExecutor CreateExecutor() => new NiDaqSyncStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Start: {s.TaskName} ({s.AiChannels.Count} AI + {s.EncoderChannels.Count} Enc @ {s.SampleRate}Hz)";
    }
}
