using System.Text.Json.Serialization;

namespace NiDaq.Models;

/// <summary>AI 终端配置</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiTerminalConfig
{
    Differential,
    RSE,
    NRSE,
    Pseudodifferential
}

/// <summary>编码器解码类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EncoderDecodingType
{
    X1,
    X2,
    X4
}

/// <summary>编码器输出单位</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EncoderUnit
{
    Pulses,
    Degrees,
    Millimeters
}

/// <summary>导出格式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DaqExportFormat
{
    Csv,
    Tdms,
    Variable,
    CsvAndVariable,
    TdmsAndVariable
}

/// <summary>触发边沿</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerEdge
{
    Rising,
    Falling
}

/// <summary>AI 采样模式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiSampleMode
{
    FiniteSamples,
    ContinuousSamples
}
