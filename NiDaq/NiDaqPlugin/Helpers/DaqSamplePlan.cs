using NiDaq.Models;
namespace NiDaq.Helpers;

public sealed record DaqSamplePlan(string CounterClockSource, int CounterCount)
{
    public static DaqSamplePlan Create(NiDaqSyncConfigSetting setting)
    {
        if (setting.AiChannels.Count == 0 || setting.EncoderChannels.Count == 0) throw new ArgumentException("同步采集需要 AI 与编码器通道");
        var devices = setting.AiChannels.SelectMany(c => Devices(c.PhysicalChannel)).Concat(setting.EncoderChannels.SelectMany(c => Devices(c.CounterChannel))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (devices.Length != 1) throw new NotSupportedException("当前同步采集仅支持同一设备内 AI SampleClock 路由；跨设备需要专门时钟/触发方案");
        if (setting.EncoderChannels.Select(c => c.CounterChannel.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != setting.EncoderChannels.Count) throw new ArgumentException("编码器 Counter 通道不能重复");
        if (setting.EncoderChannels.Any(c => c.CounterChannel.Contains(',') || c.CounterChannel.Contains(':'))) throw new NotSupportedException("每个编码器条目必须指定单个 Counter 通道");
        return new DaqSamplePlan($"/{devices[0]}/ai/SampleClock", setting.EncoderChannels.Count);
    }
    private static IEnumerable<string> Devices(string channels)
    {
        foreach (var channel in channels.Split(',', StringSplitOptions.TrimEntries))
        {
            var text = channel.Trim('/'); int split = text.IndexOf('/');
            if (split <= 0 || split == text.Length - 1) throw new ArgumentException($"非法物理通道: {channel}");
            yield return text[..split];
        }
    }
}
