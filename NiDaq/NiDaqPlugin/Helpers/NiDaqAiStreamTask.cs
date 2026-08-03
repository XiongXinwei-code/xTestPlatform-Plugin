using System.Collections.Concurrent;
using NationalInstruments.DAQmx;
using NiDaq.Models;

namespace NiDaq.Helpers;

/// <summary>AI 连续采集后台任务，边采边写磁盘</summary>
public sealed class NiDaqAiStreamTask : IDisposable
{
    private readonly NationalInstruments.DAQmx.Task _daqTask;
    private readonly AnalogMultiChannelReader _reader;
    private readonly string _filePath;
    private readonly string[] _channelNames;
    private readonly int _batchSize;
    private readonly int _maxDurationMs;
    private readonly DaqExportFormat _exportFormat;
    private readonly CancellationTokenSource _cts = new();
    private Task? _backgroundTask;
    private StreamWriter? _csvWriter;
    private NationalInstruments.Tdms.TdmsFile? _tdmsFile;
    private NationalInstruments.Tdms.TdmsChannel[]? _tdmsChannels;

    // 在线统计（每通道）
    private readonly double[] _sum;
    private readonly double[] _max;
    private readonly double[] _min;
    private readonly long[] _count;
    private long _totalSamples;

    public string FilePath => _filePath;
    public bool IsRunning => _backgroundTask != null && !_backgroundTask.IsCompleted;

    public NiDaqAiStreamTask(
        NationalInstruments.DAQmx.Task daqTask,
        AnalogMultiChannelReader reader,
        string[] channelNames,
        string filePath,
        int batchSize,
        int maxDurationMs,
        DaqExportFormat exportFormat)
    {
        _daqTask = daqTask;
        _reader = reader;
        _channelNames = channelNames;
        _filePath = filePath;
        _batchSize = batchSize;
        _maxDurationMs = maxDurationMs;
        _exportFormat = exportFormat;

        int chCount = channelNames.Length;
        _sum = new double[chCount];
        _max = new double[chCount];
        _min = new double[chCount];
        _count = new long[chCount];

        for (int i = 0; i < chCount; i++)
        {
            _max[i] = double.MinValue;
            _min[i] = double.MaxValue;
        }
    }

    public void Start()
    {
        InitFileWriter();
        _daqTask.Start();
        _backgroundTask = Task.Run(() => CollectLoop(_cts.Token));
    }

    public async Task<Dictionary<string, ChannelStatistics>> StopAsync()
    {
        _cts.Cancel();
        if (_backgroundTask != null)
        {
            try { await _backgroundTask; } catch (OperationCanceledException) { }
        }

        try { _daqTask.Stop(); } catch { }
        CloseFileWriter();

        var stats = new Dictionary<string, ChannelStatistics>();
        for (int i = 0; i < _channelNames.Length; i++)
        {
            stats[_channelNames[i]] = new ChannelStatistics
            {
                Average = _count[i] > 0 ? _sum[i] / _count[i] : 0,
                Max = _max[i],
                Min = _min[i],
                Count = _count[i]
            };
        }
        return stats;
    }

    private void CollectLoop(CancellationToken ct)
    {
        var deadline = _maxDurationMs > 0
            ? DateTime.UtcNow.AddMilliseconds(_maxDurationMs)
            : DateTime.MaxValue;

        while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            try
            {
                double[,] data = _reader.ReadMultiSample(_batchSize);
                int channels = data.GetLength(0);
                int samples = data.GetLength(1);

                for (int ch = 0; ch < channels; ch++)
                {
                    for (int s = 0; s < samples; s++)
                    {
                        double v = data[ch, s];
                        _sum[ch] += v;
                        _count[ch]++;
                        if (v > _max[ch]) _max[ch] = v;
                        if (v < _min[ch]) _min[ch] = v;
                    }
                }

                WriteToFile(data, channels, samples);
                _totalSamples += samples;
            }
            catch (DaqException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (DaqException)
            {
                break;
            }
        }
    }

    private void InitFileWriter()
    {
        if (_exportFormat is DaqExportFormat.Csv or DaqExportFormat.CsvAndVariable)
        {
            var csvPath = Path.ChangeExtension(_filePath, ".csv");
            _csvWriter = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);
            _csvWriter.WriteLine("SampleIndex," + string.Join(",", _channelNames));
        }

        if (_exportFormat is DaqExportFormat.Tdms or DaqExportFormat.TdmsAndVariable)
        {
            _tdmsFile = new NationalInstruments.Tdms.TdmsFile(_filePath);
            var group = _tdmsFile.AddGroup("Acquisition");
            _tdmsChannels = new NationalInstruments.Tdms.TdmsChannel[_channelNames.Length];
            for (int i = 0; i < _channelNames.Length; i++)
            {
                _tdmsChannels[i] = group.AddChannel<double>(_channelNames[i]);
            }
        }
    }

    private void WriteToFile(double[,] data, int channels, int samples)
    {
        if (_csvWriter != null)
        {
            for (int s = 0; s < samples; s++)
            {
                _csvWriter.Write(_totalSamples + s);
                for (int ch = 0; ch < channels; ch++)
                {
                    _csvWriter.Write(',');
                    _csvWriter.Write(data[ch, s]);
                }
                _csvWriter.WriteLine();
            }
        }

        if (_tdmsChannels != null)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                var chData = new double[samples];
                for (int s = 0; s < samples; s++)
                    chData[s] = data[ch, s];
                _tdmsChannels[ch].AppendData(chData);
            }
        }
    }

    private void CloseFileWriter()
    {
        _csvWriter?.Dispose();
        _csvWriter = null;
        _tdmsFile?.Save();
        _tdmsFile?.Dispose();
        _tdmsFile = null;
    }

    public void Dispose()
    {
        _cts.Dispose();
        _daqTask.Dispose();
        CloseFileWriter();
    }
}

public class ChannelStatistics
{
    public double Average { get; set; }
    public double Max { get; set; }
    public double Min { get; set; }
    public long Count { get; set; }
}
