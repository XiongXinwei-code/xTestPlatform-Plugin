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
        "从 TDMS 文件中流式读取采集数据并执行分析，结果存入变量。" +
        "Setting 字段：FilePath(string,表达式,TDMS文件路径), ChannelName(string,表达式,要分析的通道名), " +
        "Mode(枚举:Max/Min/Average/RMS/PeakWithRef/Slope/RangeStats,默认Max), " +
        "ReferenceChannel(string,表达式,参考通道,PeakWithRef/Slope模式用), " +
        "RangeStart(double,范围起始值,RangeStats模式用), RangeEnd(double,范围结束值), " +
        "ResultVariable(string,结果变量名,写入类型:double 分析结果数值), RefAtPeakVariable(string,PeakWithRef模式下峰值对应的参考通道值存入的变量名)。";

    public override IStepExecutor CreateExecutor() => new NiDaqDataAnalyzeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Analyze: {s.ChannelName} [{s.Mode}] → {s.ResultVariable}";
    }
}
