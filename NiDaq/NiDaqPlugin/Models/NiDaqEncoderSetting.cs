using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

/// <summary>编码器读取设置（硬件参数由 EncoderConfig 配置）</summary>
[MessagePackObject(true)]
public class NiDaqEncoderSetting
{
    /// <summary>任务名称（与 EncoderConfig 中创建的任务对应）</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>读取超时 (ms)，-1 为无限等待</summary>
    public int ReadTimeoutMs { get; set; } = 10000;

    /// <summary>结果存入的变量名</summary>
    public string ResultVariable { get; set; } = string.Empty;
}
