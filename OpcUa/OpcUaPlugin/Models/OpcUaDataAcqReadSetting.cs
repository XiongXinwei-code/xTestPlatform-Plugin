using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 数据采集读取步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaDataAcqReadSetting
{
    /// <summary>要读取的采集任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"DataAcq1\"";

    /// <summary>读取记录条数，-1 表示读取当前全部可用数据</summary>
    public int SamplesToRead { get; set; } = -1;

    /// <summary>本次读取、消费排队、转换和导出共用的总期限；旧 map 缺键时使用 5000 ms。</summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>结果变量名（波形类型 Waveform）</summary>
    [VariablePathField]
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>逐点记录 JSON 字符串变量，保留质量、源/服务器时间、主机接收 UTC 和单调时间。</summary>
    [VariablePathField]
    public string RecordsJsonVariable { get; set; } = string.Empty;

    /// <summary>健康诊断 JSON 字符串变量，包括任务真实退出、连续失败和最近错误。</summary>
    [VariablePathField]
    public string HealthVariable { get; set; } = string.Empty;

    /// <summary>是否将读取的数据追加保存到 CSV 文件</summary>
    public bool SaveToFile { get; set; } = false;

    /// <summary>CSV 文件路径（支持表达式），追加写入</summary>
    [ExpressionField]
    public string CsvFilePath { get; set; } = string.Empty;
}
