using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDataAnalyzePlugin : StepPluginBase<NiDaqDataAnalyzeSetting>
{
    public override string StepTypeId => "NiDaq.DataAnalyze";
    public override string DisplayName => "NiDaq_Data_Analyze";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description =>
        "从 TDMS 文件中流式读取采集数据并执行分析（Max/Min/Avg/RMS/峰值/斜率/区间统计），结果存入变量。" +
        "Setting 字段：FilePath(string,表达式,TDMS文件路径), ChannelName(string,表达式,通道名), " +
        "Mode(枚举Max/Min/Average/RMS/PeakWithRef/Slope/RangeStats), " +
        "ReferenceChannel(string,表达式,参考通道), RangeStart(double), RangeEnd(double), " +
        "ResultVariable(string,表达式), RefAtPeakVariable(string,表达式,PeakWithRef模式用)。";

    public override IStepExecutor CreateExecutor() => new NiDaqDataAnalyzeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Analyze: {s.ChannelName} [{s.Mode}] → {s.ResultVariable}";
    }
}
