using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using Syncfusion.UI.Xaml.Charts;
using WaveformAnalysisPlugin.Analysis;
using WaveformAnalysisPlugin.UI.ViewModels;

namespace WaveformAnalysisPlugin.UI.Views
{
    public partial class WaveformChartWindow : Window
    {
        public WaveformChartWindow(string resultJson, double sampleRate)
        {
            InitializeComponent();

            try
            {
                var result = JsonSerializer.Deserialize<WaveformAnalysisResult>(resultJson);
                if (result != null)
                    RenderChart(result, sampleRate);
            }
            catch (Exception ex)
            {
                TxtInfo.Text = $"解析结果失败: {ex.Message}";
            }
        }

        private void RenderChart(WaveformAnalysisResult result, double sampleRate)
        {
            if (result.FrequencyAxis is { Length: > 0 })
            {
                XAxis.Header = "频率 (Hz)";
                var data = new ObservableCollection<ChartDataPoint>();
                for (int i = 0; i < result.FrequencyAxis.Length; i++)
                    data.Add(new ChartDataPoint { X = result.FrequencyAxis[i], Y = result.ProcessedData[i] });

                Chart.Series.Add(new FastLineSeries
                {
                    ItemsSource = data,
                    XBindingPath = "X",
                    YBindingPath = "Y",
                    Label = "频谱幅值"
                });
                TxtInfo.Text = $"频谱分析: {result.FrequencyAxis.Length} 个频率点";
            }
            else
            {
                XAxis.Header = "时间 (s)";

                var original = new ObservableCollection<ChartDataPoint>();
                for (int i = 0; i < result.OriginalData.Length; i++)
                    original.Add(new ChartDataPoint { X = i / sampleRate, Y = result.OriginalData[i] });
                Chart.Series.Add(new FastLineSeries
                {
                    ItemsSource = original,
                    XBindingPath = "X",
                    YBindingPath = "Y",
                    Label = "原始",
                    Interior = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                });

                if (result.ProcessedData.Length > 0)
                {
                    var processed = new ObservableCollection<ChartDataPoint>();
                    for (int i = 0; i < result.ProcessedData.Length; i++)
                        processed.Add(new ChartDataPoint { X = i / sampleRate, Y = result.ProcessedData[i] });
                    Chart.Series.Add(new FastLineSeries
                    {
                        ItemsSource = processed,
                        XBindingPath = "X",
                        YBindingPath = "Y",
                        Label = "处理后",
                        Interior = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DodgerBlue)
                    });
                }

                if (result.PeakIndices.Count > 0)
                {
                    var peaks = new ObservableCollection<ChartDataPoint>();
                    foreach (var idx in result.PeakIndices)
                        peaks.Add(new ChartDataPoint { X = idx / sampleRate, Y = result.OriginalData[idx] });
                    Chart.Series.Add(new ScatterSeries
                    {
                        ItemsSource = peaks,
                        XBindingPath = "X",
                        YBindingPath = "Y",
                        Label = "峰值",
                        ScatterHeight = 8,
                        ScatterWidth = 8,
                        Interior = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red)
                    });
                }

                if (result.Statistics != null)
                    TxtInfo.Text = $"均值={result.Statistics.Mean:F4}  RMS={result.Statistics.RMS:F4}  峰峰值={result.Statistics.PeakToPeak:F4}";
                else
                    TxtInfo.Text = $"数据点: {result.OriginalData.Length}, 峰值: {result.PeakIndices.Count}";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
