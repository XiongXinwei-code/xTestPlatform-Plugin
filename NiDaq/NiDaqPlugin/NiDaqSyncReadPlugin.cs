using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqSyncReadPlugin : StepPluginBase<NiDaqSyncReadSetting>
{
    public override string StepTypeId => "NiDaq.SyncRead";
    public override string DisplayName => "NiDaq_Sync_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "从已启动的同步采集任务中读取 AI 和编码器对齐数据。" +
        "Setting 字段：TaskName(string,表达式), SamplesToRead(int), ResultVariable(string,表达式), ExportFormat(enum), OutputDirectory(string,表达式)。";

    public override IStepExecutor CreateExecutor() => new NiDaqSyncReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Read: {s.TaskName} → {s.ResultVariable}";
    }
}
