using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace WaveformAnalysisPlugin.Models
{
    public enum AnalysisType
    {
        [Description("FFT 频谱分析")]
        FFT,

        [Description("峰值/谷值检测")]
        PeakDetection,

        [Description("统计分析 (RMS/均值/标准差)")]
        Statistics,

        [Description("低通滤波")]
        LowPassFilter,

        [Description("高通滤波")]
        HighPassFilter,

        [Description("带通滤波")]
        BandPassFilter
    }

    [MessagePackObject(true)]
    public class WaveformAnalysisSetting : INotifyPropertyChanged
    {
        private AnalysisType _analysisType = AnalysisType.FFT;
        private double _sampleRate = 1000.0;
        private double _cutoffFrequencyLow = 50.0;
        private double _cutoffFrequencyHigh = 200.0;
        private int _filterOrder = 4;
        private double _peakThreshold = 0.5;
        private string _inputVariableName = string.Empty;
        private string _outputVariableName = string.Empty;
        private bool _showChartOnExecution = true;

        /// <summary>分析类型</summary>
        public AnalysisType AnalysisType
        {
            get => _analysisType;
            set => SetProperty(ref _analysisType, value);
        }

        /// <summary>采样率 (Hz)</summary>
        public double SampleRate
        {
            get => _sampleRate;
            set => SetProperty(ref _sampleRate, value);
        }

        /// <summary>低截止频率 (Hz) - 用于低通/带通</summary>
        public double CutoffFrequencyLow
        {
            get => _cutoffFrequencyLow;
            set => SetProperty(ref _cutoffFrequencyLow, value);
        }

        /// <summary>高截止频率 (Hz) - 用于高通/带通</summary>
        public double CutoffFrequencyHigh
        {
            get => _cutoffFrequencyHigh;
            set => SetProperty(ref _cutoffFrequencyHigh, value);
        }

        /// <summary>滤波器阶数</summary>
        public int FilterOrder
        {
            get => _filterOrder;
            set => SetProperty(ref _filterOrder, value);
        }

        /// <summary>峰值检测阈值</summary>
        public double PeakThreshold
        {
            get => _peakThreshold;
            set => SetProperty(ref _peakThreshold, value);
        }

        /// <summary>输入数据步骤变量名</summary>
        public string InputVariableName
        {
            get => _inputVariableName;
            set => SetProperty(ref _inputVariableName, value);
        }

        /// <summary>输出结果步骤变量名</summary>
        public string OutputVariableName
        {
            get => _outputVariableName;
            set => SetProperty(ref _outputVariableName, value);
        }

        /// <summary>执行时是否弹出图表窗口</summary>
        public bool ShowChartOnExecution
        {
            get => _showChartOnExecution;
            set => SetProperty(ref _showChartOnExecution, value);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
