using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[MessagePackObject(true)]
public class NiDaqSyncConfigSetting
{
    /// <summary>任务名称</summary>
    [ExpressionField]
    public string TaskName { get; set; } = string.Empty;

    /// <summary>AI 通道列表</summary>
    public ObservableCollection<NiDaqAiChannel> AiChannels { get; set; } = new();

    /// <summary>编码器通道列表</summary>
    public ObservableCollection<NiDaqSyncEncoderChannel> EncoderChannels { get; set; } = new();

    /// <summary>采样率 (Hz)</summary>
    public double SampleRate { get; set; } = 1000;

    /// <summary>采样数（每通道）</summary>
    public int SamplesPerChannel { get; set; } = 100;

    /// <summary>采样模式</summary>
    public AiSampleMode SampleMode { get; set; } = AiSampleMode.FiniteSamples;

    /// <summary>时钟源（空为内部时钟）</summary>
    public string ClockSource { get; set; } = string.Empty;

    /// <summary>是否使用触发</summary>
    public bool UseTrigger { get; set; } = false;

    /// <summary>触发源</summary>
    public string TriggerSource { get; set; } = string.Empty;

    /// <summary>触发边沿</summary>
    public TriggerEdge TriggerEdge { get; set; } = TriggerEdge.Rising;
}
