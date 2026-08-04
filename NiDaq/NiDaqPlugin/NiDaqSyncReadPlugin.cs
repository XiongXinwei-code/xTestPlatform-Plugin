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
        "从已启动的同步采集任务中读取 AI 和编码器对齐数据，可导出为文件并/或将统计值存入变量。" +
        "Setting 字段：TaskName(string,表达式,要读取的同步任务名), SamplesToRead(int,读取样本数,-1=读取所有可用,默认-1), " +
        "ReadTimeoutMs(int,读取超时ms,默认10000), ResultVariable(string,表达式,统计结果存入的变量名), " +
        "ExportFormat(枚举:Csv/Tdms/Variable/CsvAndVariable/TdmsAndVariable,默认Csv), " +
        "SaveToFile(bool,默认false), OutputDirectory(string,表达式), MaxFileSizeMB(int,默认500), " +
        "EnableCustomEvent(bool,默认false), CustomEventName(string,默认SyncDataReady)。";

    public override IStepExecutor CreateExecutor() => new NiDaqSyncReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Sync Read: {s.TaskName} → {s.ResultVariable}";
    }
}
