using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqDataAnalyzeSetting
{
    /// <summary>TDMS/CSV 文件路径表达式</summary>
    [ExpressionField]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>要分析的通道名称</summary>
    [ExpressionField]
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>分析模式</summary>
    public AnalyzeMode Mode { get; set; } = AnalyzeMode.Max;

    /// <summary>参考通道（用于区间过滤或 PeakWithRef）</summary>
    [ExpressionField]
    public string ReferenceChannel { get; set; } = string.Empty;

    /// <summary>区间起始值</summary>
    public double RangeStart { get; set; } = 0;

    /// <summary>区间结束值</summary>
    public double RangeEnd { get; set; } = 0;

    /// <summary>结果存入的变量名</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>PeakWithRef 模式下，峰值对应参考值存入的变量名</summary>
    [ExpressionField]
    public string RefAtPeakVariable { get; set; } = string.Empty;
}
