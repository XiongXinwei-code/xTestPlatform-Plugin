using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WaveformAnalysisPlugin.Analysis;
using WaveformAnalysisPlugin.Models;
using xTestPlatform.Core.SequenceModels;

namespace WaveformAnalysisPlugin.UI.ViewModels
{
    public partial class ChartDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public partial class WaveformAnalysisEditorViewModel : ObservableObject
    {
        private readonly Step _step;
        private readonly WaveformAnalysisSetting _setting;
        private double[]? _previewData;

        private static readonly MessagePack.MessagePackSerializerOptions _opts =
            MessagePack.MessagePackSerializerOptions.Standard
                .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

        public WaveformAnalysisEditorViewModel(xTestPlatform.Core.SequenceModels.Step step)
        {
            _step = step;
            var raw = step.StepSetting?.Setting;
            _setting = raw is { Length: > 0 }
                ? MessagePack.MessagePackSerializer.Deserialize<WaveformAnalysisSetting>(raw, _opts)
                : new WaveformAnalysisSetting();

            // 初始化属性
            _analysisType = _setting.AnalysisType;
            _sampleRate = _setting.SampleRate;
            _inputVariableName = _setting.InputVariableName;
            _outputVariableName = _setting.OutputVariableName;
            _cutoffFrequencyLow = _setting.CutoffFrequencyLow;
            _cutoffFrequencyHigh = _setting.CutoffFrequencyHigh;
            _peakThreshold = _setting.PeakThreshold;
            _filterOrder = _setting.FilterOrder;
            _showChartOnExecution = _setting.ShowChartOnExecution;
        }

        public Array AnalysisTypes => Enum.GetValues<AnalysisType>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFilterVisible))]
        [NotifyPropertyChangedFor(nameof(IsPeakVisible))]
        private AnalysisType _analysisType;

        [ObservableProperty] private double _sampleRate;
        [ObservableProperty] private string _inputVariableName = string.Empty;
        [ObservableProperty] private string _outputVariableName = string.Empty;
        [ObservableProperty] private double _cutoffFrequencyLow;
        [ObservableProperty] private double _cutoffFrequencyHigh;
        [ObservableProperty] private double _peakThreshold;
        [ObservableProperty] private int _filterOrder;
        [ObservableProperty] private bool _showChartOnExecution;
        [ObservableProperty] private string _csvInfo = string.Empty;
        [ObservableProperty] private string _statisticsText = string.Empty;

        [ObservableProperty] private ObservableCollection<ChartDataPoint> _originalSeries = [];
        [ObservableProperty] private ObservableCollection<ChartDataPoint> _processedSeries = [];
        [ObservableProperty] private ObservableCollection<ChartDataPoint> _peakMarkers = [];

        public bool IsFilterVisible => AnalysisType is AnalysisType.LowPassFilter
            or AnalysisType.HighPassFilter or AnalysisType.BandPassFilter;
        public bool IsPeakVisible => AnalysisType == AnalysisType.PeakDetection;

        partial void OnAnalysisTypeChanged(AnalysisType value) { _setting.AnalysisType = value; Save(); UpdatePreview(); }
        partial void OnSampleRateChanged(double value) { _setting.SampleRate = value; Save(); UpdatePreview(); }
        partial void OnInputVariableNameChanged(string value) { _setting.InputVariableName = value; Save(); }
        partial void OnOutputVariableNameChanged(string value) { _setting.OutputVariableName = value; Save(); }
        partial void OnCutoffFrequencyLowChanged(double value) { _setting.CutoffFrequencyLow = value; Save(); UpdatePreview(); }
        partial void OnCutoffFrequencyHighChanged(double value) { _setting.CutoffFrequencyHigh = value; Save(); UpdatePreview(); }
        partial void OnPeakThresholdChanged(double value) { _setting.PeakThreshold = value; Save(); UpdatePreview(); }
        partial void OnFilterOrderChanged(int value) { _setting.FilterOrder = value; Save(); UpdatePreview(); }
        partial void OnShowChartOnExecutionChanged(bool value) { _setting.ShowChartOnExecution = value; Save(); }

        [RelayCommand]
        private void ImportCsv()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                Title = "导入波形数据"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName);
                var data = new List<double>();
                foreach (var line in lines)
                {
                    var parts = line.Split(',', ';', '\t');
                    foreach (var part in parts)
                    {
                        if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                            data.Add(val);
                    }
                }

                _previewData = data.ToArray();
                CsvInfo = $"已加载 {_previewData.Length} 个采样点 ({Path.GetFileName(dlg.FileName)})";
                UpdatePreview();
            }
            catch (Exception ex)
            {
                CsvInfo = $"导入失败: {ex.Message}";
            }
        }

        private void UpdatePreview()
        {
            if (_previewData == null || _previewData.Length == 0) return;

            WaveformAnalysisResult result = AnalysisType switch
            {
                AnalysisType.FFT => WaveformAnalyzer.PerformFFT(_previewData, SampleRate),
                AnalysisType.PeakDetection => WaveformAnalyzer.DetectPeaks(_previewData, PeakThreshold),
                AnalysisType.Statistics => WaveformAnalyzer.ComputeStatistics(_previewData),
                AnalysisType.LowPassFilter => WaveformAnalyzer.LowPassFilter(_previewData, SampleRate, CutoffFrequencyLow, FilterOrder),
                AnalysisType.HighPassFilter => WaveformAnalyzer.HighPassFilter(_previewData, SampleRate, CutoffFrequencyHigh, FilterOrder),
                AnalysisType.BandPassFilter => WaveformAnalyzer.BandPassFilter(_previewData, SampleRate, CutoffFrequencyLow, CutoffFrequencyHigh, FilterOrder),
                _ => WaveformAnalyzer.ComputeStatistics(_previewData)
            };

            OriginalSeries.Clear();
            ProcessedSeries.Clear();
            PeakMarkers.Clear();

            if (AnalysisType == AnalysisType.FFT)
            {
                for (int i = 0; i < result.FrequencyAxis.Length; i++)
                    ProcessedSeries.Add(new ChartDataPoint { X = result.FrequencyAxis[i], Y = result.ProcessedData[i] });
            }
            else
            {
                for (int i = 0; i < result.OriginalData.Length; i++)
                    OriginalSeries.Add(new ChartDataPoint { X = i / SampleRate, Y = result.OriginalData[i] });

                for (int i = 0; i < result.ProcessedData.Length; i++)
                    ProcessedSeries.Add(new ChartDataPoint { X = i / SampleRate, Y = result.ProcessedData[i] });

                foreach (var idx in result.PeakIndices)
                    PeakMarkers.Add(new ChartDataPoint { X = idx / SampleRate, Y = result.OriginalData[idx] });
            }

            if (result.Statistics != null)
            {
                var s = result.Statistics;
                StatisticsText = $"均值: {s.Mean:F4} | RMS: {s.RMS:F4} | 标准差: {s.StandardDeviation:F4} | 峰峰值: {s.PeakToPeak:F4}";
            }
            else
            {
                StatisticsText = string.Empty;
            }
        }

        private void Save()
        {
            var bytes = MessagePack.MessagePackSerializer.Serialize(_setting, _opts);
            if (_step.StepSetting != null)
                _step.StepSetting.Setting = bytes;
        }
    }
}
