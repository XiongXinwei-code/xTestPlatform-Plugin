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
        "从已启动的 AI 采集任务中读取数据，可导出为文件并/或将统计值存入变量。" +
        "Setting 字段：TaskName(string,表达式,要读取的任务名), SamplesToRead(int,读取样本数,-1=读取所有可用,默认-1), " +
        "ReadTimeoutMs(int,读取超时ms,-1=无限等待,默认10000), " +
        "ResultVariable(string,结果变量名,写入类型:double[,] 二维数组,每行为一个通道的采样序列), " +
        "ExportFormat(枚举:Csv/Tdms/Variable/CsvAndVariable/TdmsAndVariable,默认Csv), " +
        "SaveToFile(bool,是否将采集数据保存到文件,默认false), OutputDirectory(string,表达式,输出文件目录,空=默认数据目录), " +
        "MaxFileSizeMB(int,单文件大小上限MB,超过后自动轮转,默认500), " +
        "EnableCustomEvent(bool,是否启用自定义事件发送采集数据,默认false), CustomEventName(string,自定义事件名称,默认AiDataReady)。";

    public override IStepExecutor CreateExecutor() => new NiDaqAiReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Read: {s.TaskName} → {s.ResultVariable}";
    }
}
