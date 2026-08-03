using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqSyncStopPlugin : StepPluginBase<NiDaqSyncStopSetting>
{
    public override string StepTypeId => "NiDaq.SyncStop";
    public override string DisplayName => "NiDaq_Sync_Stop";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "停止 NI DAQ 同步采集任务，关闭文件流，将统计值和文件路径写入变量。" +
        "Setting 字段：TaskName(string,表达式,要停止的同步任务名)。";

    public override IStepExecutor CreateExecutor() => new NiDaqSyncStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Stop: {s.TaskName}";
    }
}
