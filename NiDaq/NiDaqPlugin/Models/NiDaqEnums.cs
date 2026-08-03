namespace NiDaq.Models;

/// <summary>AI 终端配置</summary>
public enum AiTerminalConfig
{
    Differential,
    RSE,
    NRSE,
    Pseudodifferential
}

/// <summary>编码器解码类型</summary>
public enum EncoderDecodingType
{
    X1,
    X2,
    X4
}

/// <summary>编码器输出单位</summary>
public enum EncoderUnit
{
    Pulses,
    Degrees,
    Millimeters
}

/// <summary>导出格式</summary>
public enum DaqExportFormat
{
    Csv,
    Tdms,
    Variable,
    CsvAndVariable,
    TdmsAndVariable
}

/// <summary>数据分析模式</summary>
public enum AnalyzeMode
{
    Max,
    Min,
    Average,
    RMS,
    PeakWithRef,
    Slope,
    RangeStats
}

/// <summary>触发边沿</summary>
public enum TriggerEdge
{
    Rising,
    Falling
}
