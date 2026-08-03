using NationalInstruments.Tdms;
using NationalInstruments.DAQmx;
using NiDaq.Models;
using DaqTask = NationalInstruments.DAQmx.Task;

namespace NiDaq.Helpers;

/// <summary>同步采集后台任务（AI + 编码器共享时钟），边采边写磁盘</summary>
public sealed class NiDaqSyncStreamTask : IDisposable
{
    private readonly DaqTask _aiTask;
    private readonly DaqTask _ciTask;
    private readonly AnalogMultiChannelReader _aiReader;
    private readonly CounterMultiChannelReader _ciReader;
    private readonly string _filePath;
    private readonly string[] _aiChannelNames;
    private readonly string[] _encoderChannelNames;
    private readonly double[] _distancePerPulse;
    private readonly int _batchSize;
    private readonly int _maxDurationMs;
    private readonly DaqExportFormat _exportFormat;
    private readonly CancellationTokenSource _cts = new();
    private System.Threading.Tasks.Task? _backgroundTask;
    private StreamWriter? _csvWriter;
    private NationalInstruments.Tdms.TdmsFile? _tdmsFile;
    private NationalInstruments.Tdms.TdmsChannel[]? _tdmsAiChannels;
    private NationalInstruments.Tdms.TdmsChannel[]? _tdmsEncoderChannels;

    // 在线统计
    private readonly double[] _aiSum;
    private readonly double[] _aiMax;
    private readonly double[] _aiMin;
    private readonly long[] _aiCount;
    private readonly double[] _encSum;
    private readonly double[] _encMax;
    private readonly double[] _encMin;
    private readonly long[] _encCount;
    private long _totalSamples;

    public string FilePath => _filePath;
    public bool IsRunning => _backgroundTask != null && !_backgroundTask.IsCompleted;

    public NiDaqSyncStreamTask(
        DaqTask aiTask,
        DaqTask ciTask,
        AnalogMultiChannelReader aiReader,
        CounterMultiChannelReader ciReader,
        string[] aiChannelNames,
        string[] encoderChannelNames,
        double[] distancePerPulse,
        string filePath,
        int batchSize,
        int maxDurationMs,
        DaqExportFormat exportFormat)
    {
        _aiTask = aiTask;
        _ciTask = ciTask;
        _aiReader = aiReader;
        _ciReader = ciReader;
        _aiChannelNames = aiChannelNames;
        _encoderChannelNames = encoderChannelNames;
        _distancePerPulse = distancePerPulse;
        _filePath = filePath;
        _batchSize = batchSize;
        _maxDurationMs = maxDurationMs;
        _exportFormat = exportFormat;

        _aiSum = new double[aiChannelNames.Length];
        _aiMax = new double[aiChannelNames.Length];
        _aiMin = new double[aiChannelNames.Length];
        _aiCount = new long[aiChannelNames.Length];
        _encSum = new double[encoderChannelNames.Length];
        _encMax = new double[encoderChannelNames.Length];
        _encMin = new double[encoderChannelNames.Length];
        _encCount = new long[encoderChannelNames.Length];

        for (int i = 0; i < aiChannelNames.Length; i++) { _aiMax[i] = double.MinValue; _aiMin[i] = double.MaxValue; }
        for (int i = 0; i < encoderChannelNames.Length; i++) { _encMax[i] = double.MinValue; _encMin[i] = double.MaxValue; }
    }

    public void Start()
    {
        InitFileWriter();
        _ciTask.Start();
        _aiTask.Start();
        _backgroundTask = System.Threading.Tasks.Task.Run(() => CollectLoop(_cts.Token));
    }

    public async Task<Dictionary<string, ChannelStatistics>> StopAsync()
    {
        _cts.Cancel();
        if (_backgroundTask != null)
        {
            try { await _backgroundTask; } catch (OperationCanceledException) { }
        }

        try { _aiTask.Stop(); } catch { }
        try { _ciTask.Stop(); } catch { }
        CloseFileWriter();

        var stats = new Dictionary<string, ChannelStatistics>();
        for (int i = 0; i < _aiChannelNames.Length; i++)
        {
            stats[_aiChannelNames[i]] = new ChannelStatistics
            {
                Average = _aiCount[i] > 0 ? _aiSum[i] / _aiCount[i] : 0,
                Max = _aiMax[i], Min = _aiMin[i], Count = _aiCount[i]
            };
        }
        for (int i = 0; i < _encoderChannelNames.Length; i++)
        {
            stats[_encoderChannelNames[i]] = new ChannelStatistics
            {
                Average = _encCount[i] > 0 ? _encSum[i] / _encCount[i] : 0,
                Max = _encMax[i], Min = _encMin[i], Count = _encCount[i]
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
                double[,] aiData = _aiReader.ReadMultiSample(_batchSize);
                double[,] ciData = _ciReader.ReadMultiSampleDouble(_batchSize);

                int aiCh = aiData.GetLength(0);
                int encCh = ciData.GetLength(0);
                int samples = aiData.GetLength(1);

                // 更新 AI 统计
                for (int ch = 0; ch < aiCh; ch++)
                {
                    for (int s = 0; s < samples; s++)
                    {
                        double v = aiData[ch, s];
                        _aiSum[ch] += v; _aiCount[ch]++;
                        if (v > _aiMax[ch]) _aiMax[ch] = v;
                        if (v < _aiMin[ch]) _aiMin[ch] = v;
                    }
                }

                // 编码器：脉冲转换为物理量
                var encoderConverted = new double[encCh, samples];
                for (int ch = 0; ch < encCh; ch++)
                {
                    double dpp = ch < _distancePerPulse.Length ? _distancePerPulse[ch] : 1.0;
                    for (int s = 0; s < samples; s++)
                    {
                        double v = ciData[ch, s] * dpp;
                        encoderConverted[ch, s] = v;
                        _encSum[ch] += v; _encCount[ch]++;
                        if (v > _encMax[ch]) _encMax[ch] = v;
                        if (v < _encMin[ch]) _encMin[ch] = v;
                    }
                }

                WriteToFile(aiData, encoderConverted, aiCh, encCh, samples);
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
        var allNames = _aiChannelNames.Concat(_encoderChannelNames).ToArray();

        if (_exportFormat is DaqExportFormat.Csv or DaqExportFormat.CsvAndVariable)
        {
            var csvPath = Path.ChangeExtension(_filePath, ".csv");
            _csvWriter = new StreamWriter(csvPath, false, System.Text.Encoding.UTF8);
            _csvWriter.WriteLine("SampleIndex," + string.Join(",", allNames));
        }

        if (_exportFormat is DaqExportFormat.Tdms or DaqExportFormat.TdmsAndVariable)
        {
            _tdmsFile = new NationalInstruments.Tdms.TdmsFile(_filePath);
            var group = _tdmsFile.AddChannelGroup("SyncAcquisition");
            _tdmsAiChannels = new NationalInstruments.Tdms.TdmsChannel[_aiChannelNames.Length];
            _tdmsEncoderChannels = new NationalInstruments.Tdms.TdmsChannel[_encoderChannelNames.Length];
            for (int i = 0; i < _aiChannelNames.Length; i++)
                _tdmsAiChannels[i] = group.AddChannel(_aiChannelNames[i], TdmsDataType.Double);
            for (int i = 0; i < _encoderChannelNames.Length; i++)
                _tdmsEncoderChannels[i] = group.AddChannel(_encoderChannelNames[i], TdmsDataType.Double);
        }
    }

    private void WriteToFile(double[,] aiData, double[,] encData, int aiCh, int encCh, int samples)
    {
        if (_csvWriter != null)
        {
            for (int s = 0; s < samples; s++)
            {
                _csvWriter.Write(_totalSamples + s);
                for (int ch = 0; ch < aiCh; ch++) { _csvWriter.Write(','); _csvWriter.Write(aiData[ch, s]); }
                for (int ch = 0; ch < encCh; ch++) { _csvWriter.Write(','); _csvWriter.Write(encData[ch, s]); }
                _csvWriter.WriteLine();
            }
        }

        if (_tdmsAiChannels != null)
        {
            for (int ch = 0; ch < aiCh; ch++)
            {
                var chData = new double[samples];
                for (int s = 0; s < samples; s++) chData[s] = aiData[ch, s];
                _tdmsAiChannels[ch].AppendData(chData);
            }
        }
        if (_tdmsEncoderChannels != null)
        {
            for (int ch = 0; ch < encCh; ch++)
            {
                var chData = new double[samples];
                for (int s = 0; s < samples; s++) chData[s] = encData[ch, s];
                _tdmsEncoderChannels[ch].AppendData(chData);
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
        _aiTask.Dispose();
        _ciTask.Dispose();
        CloseFileWriter();
    }
}
