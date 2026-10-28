using NationalInstruments.DAQmx;
using NiDaq.Models;
using xTestPlatform.Core.Models;

namespace NiDaq.Helpers;

internal sealed record DaqReadResult(double[,] Data, string[] Names, double SampleRate)
{
    public WaveformData ToWaveform(string name)
    {
        var waveform = new WaveformData { TaskID = name, SampleRate = SampleRate, StartTime = DateTime.MinValue, Channels = new List<ChannelData>() };
        for (int channel = 0; channel < Data.GetLength(0); channel++)
        {
            var samples = new double[Data.GetLength(1)];
            for (int i = 0; i < samples.Length; i++) samples[i] = Data[channel, i];
            waveform.Channels.Add(new ChannelData { Channel = Names[channel], Values = samples });
        }
        return waveform;
    }
}

internal static class DaqReadCore
{
    public static DaqReadResult Read(DaqTaskResource resource, int requestedSamples, int timeoutMs, bool sync)
    {
        resource.EnsureOperationActive();
        resource.RequireStarted();
        if (requestedSamples != -1 && requestedSamples <= 0) throw new ArgumentOutOfRangeException(nameof(requestedSamples));
        if (timeoutMs != -1 && timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        var ai = resource.Primary;
        if (ai.AIChannels.Count == 0 || sync != resource.IsSync) throw new InvalidOperationException("读取步骤与已配置任务类型不一致");
        var counters = resource.Counters;
        int channels = ai.AIChannels.Count + counters.Count;
        long available = ai.Stream.AvailableSamplesPerChannel;
        foreach (var counter in counters) { resource.EnsureOperationActive(); available = Math.Min(available, counter.Stream.AvailableSamplesPerChannel); }
        int count = requestedSamples == -1 ? checked((int)available) : requestedSamples;
        if ((long)count * channels > 16_000_000) throw new ArgumentOutOfRangeException(nameof(requestedSamples), "一次读取总样本数不能超过 1600 万");
        var names = Enumerable.Range(0, ai.AIChannels.Count).Select(i => ai.AIChannels[i].VirtualName).Concat(resource.CounterNames).ToArray();
        if (names.Length != channels) throw new InvalidOperationException("任务通道元数据不完整");
        if (count == 0) return new DaqReadResult(new double[channels, 0], names, ai.Timing.SampleClockRate);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int Remaining()
        {
            resource.EnsureOperationActive();
            if (timeoutMs == -1) return -1;
            int remaining = (int)Math.Max(0, timeoutMs - clock.ElapsedMilliseconds);
            if (remaining == 0) throw new TimeoutException("DAQ 复合读取总期限已到");
            return remaining;
        }
        ai.Stream.Timeout = Remaining();
        resource.EnsureOperationActive();
        var analog = new AnalogMultiChannelReader(ai.Stream).ReadMultiSample(count);
        if (analog.GetLength(0) != ai.AIChannels.Count || analog.GetLength(1) != count) throw new IOException("AI 返回的样本数与请求不一致");
        var data = new double[channels, count];
        for (int ch = 0; ch < ai.AIChannels.Count; ch++) for (int i = 0; i < count; i++) data[ch, i] = analog[ch, i];
        for (int ch = 0; ch < counters.Count; ch++)
        {
            counters[ch].Stream.Timeout = Remaining();
            resource.EnsureOperationActive();
            var samples = new CounterSingleChannelReader(counters[ch].Stream).ReadMultiSampleDouble(count);
            if (samples.Length != count) throw new IOException("CI 返回的样本数与 AI 不一致，不能伪装同步结果");
            for (int i = 0; i < count; i++) data[ai.AIChannels.Count + ch, i] = samples[i];
        }
        return new DaqReadResult(data, names, ai.Timing.SampleClockRate);
    }
}

public static class DaqExportPolicy
{
    public static bool ValidateReadRequest(DaqExportFormat format, bool saveToFile, string resultVariable, int samples, int timeoutMs, int maxFileSizeMB)
    {
        bool save = Validate(format, saveToFile);
        if (!save && string.IsNullOrWhiteSpace(resultVariable)) throw new ArgumentException("必须提供结果变量或启用 CSV 存盘，不能无输出地丢弃采样数据");
        if (samples != -1 && samples <= 0) throw new ArgumentOutOfRangeException(nameof(samples));
        if (timeoutMs != -1 && timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (save && maxFileSizeMB <= 0) throw new ArgumentOutOfRangeException(nameof(maxFileSizeMB));
        return save;
    }
    public static bool Validate(DaqExportFormat format, bool saveToFile)
    {
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (format is DaqExportFormat.Tdms or DaqExportFormat.TdmsAndVariable)
            throw new NotSupportedException("当前插件未实现 TDMS 写入，请选择 CSV；不会将 CSV 伪装为 TDMS");
        if (format == DaqExportFormat.Variable && saveToFile)
            throw new ArgumentException("Variable 格式不能启用文件存盘，请取消存盘或选择 CSV");
        return saveToFile;
    }
}
