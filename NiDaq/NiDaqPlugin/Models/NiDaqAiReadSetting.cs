using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqAiReadSetting
{
    /// <summary>任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>读取样本数（每通道），-1 为读取所有可用</summary>
    public int SamplesToRead { get; set; } = -1;

    /// <summary>读取超时 (ms)，-1 为无限等待</summary>
    public int ReadTimeoutMs { get; set; } = 10000;

    /// <summary>结果变量名</summary>
    public string ResultVariable { get; set; } = string.Empty;

    /// <summary>导出格式</summary>
    public DaqExportFormat ExportFormat { get; set; } = DaqExportFormat.Csv;

    /// <summary>是否将采集数据保存到文件</summary>
    public bool SaveToFile { get; set; } = false;

    /// <summary>输出文件目录（为空时使用默认数据目录）</summary>
    [ExpressionField]
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>单个文件大小上限 (MB)，超过后自动轮转</summary>
    public int MaxFileSizeMB { get; set; } = 500;

    /// <summary>是否启用自定义事件发送采集数据</summary>
    public bool EnableCustomEvent { get; set; } = false;

    /// <summary>自定义事件名称</summary>
    public string CustomEventName { get; set; } = "AiDataReady";
}
