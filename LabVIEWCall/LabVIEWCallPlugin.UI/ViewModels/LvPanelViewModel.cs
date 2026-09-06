using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.Converters;
using LabVIEWCallPlugin.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressionEditor;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI.ViewModels
{
    /// <summary>
    /// LabVIEW �����ͼģ��
    /// </summary>
    public partial class LvPanelViewModel : ObservableObject
    {
        /// <summary>����������</summary>
        public class LoadPanelParameter
        {
            public string ViFilePath { get; set; } = string.Empty;
            public string ControlJson { get; set; } = string.Empty;
            public string IndicatorJson { get; set; } = string.Empty;
        }

        [ObservableProperty] private ObservableCollection<LvPanelNode> _controlNodes;
        [ObservableProperty] private ObservableCollection<LvPanelNode> _indicatorNodes;
        [ObservableProperty] private LvPanelNode? _selectedNode;
        [ObservableProperty] private ImageSource? _panelImage;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _controlJson;
        [ObservableProperty] private string _indicatorJson;
        [ObservableProperty] private bool _isReentrant;

        /// <summary>VI ��ǰ����״̬</summary>
        [ObservableProperty] private VIState _viState;

        /// <summary>VI ״̬��ʾ�ı�</summary>
        [ObservableProperty] private string _viStateText;

        /// <summary>LabVIEW IDE ����״̬</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLabViewDisconnected))]
        private LabViewIdeState _labViewIdeState;

        /// <summary>LabVIEW IDE δ����ʱΪ true�����������ʾ��ɼ��԰󶨣�</summary>
        public bool IsLabViewDisconnected =>
            LabViewIdeState == LabViewIdeState.Disconnected ||
            LabViewIdeState == LabViewIdeState.Unknown;

        /// <summary>LabVIEW IDE ״̬��ʾ�ı�</summary>
        [ObservableProperty] private string _labViewIdeStateText;

        // ��ȷ��ע�ɿգ����� NullReferenceException
        [ObservableProperty] private SequenceFile? _sequenceFile;
        [ObservableProperty] private EditPosition? _editPosition;

        private string _viFilePath = string.Empty;

        public LvPanelViewModel()
        {
            _controlNodes = new ObservableCollection<LvPanelNode>();
            _indicatorNodes = new ObservableCollection<LvPanelNode>();
            _controlJson = string.Empty;
            _indicatorJson = string.Empty;
            _viState = VIState.Idle;
            _viStateText = "Idle";
            _labViewIdeState = LabViewIdeState.Unknown;
            _labViewIdeStateText = "Unknown";

            _controlNodes.CollectionChanged += (s, e) => UpdateControlJson();
            _indicatorNodes.CollectionChanged += (s, e) => UpdateIndicatorJson();
        }

        // ���� ���� ����������������������������������������������������������������������������������������������������������������������������

        [RelayCommand]
        private void OpenVariableView(LvPanelNode? node)
        {
            // SequenceFile δ��ֵʱֱ�Ӻ��ԣ����� NullReferenceException
            if (SequenceFile is null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {

                // �� LabVIEW ����ת��Ϊƽ̨������
                string platformType = LabVIEWTypeConverter.ConvertToString(node?.Type);

                var dialog = new ExpressionEditorDialog(
                    SequenceFile,
                    EditPosition,
                    node?.Variable ?? string.Empty,
                    platformType)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                dialog.ShowDialog();

                if (node != null)
                {
                    node.Variable = dialog.ScriptText;
                    node.ValueSourceTypeEnum = ValueSourceType.Variable;
                }
            }, DispatcherPriority.Normal);
        }

        /// <summary>
        /// ���� VI ���������Ϣ��
        /// <para>
        /// �׶�һ����� LabVIEW IDE �Ƿ����ߡ�<br/>
        /// IDE δ���� �� ��̨������������ JSON �ָ��ڵ㣬await ���� UI �߳���伯�ϣ����� DLL ���á�<br/>
        /// IDE ������ �� ���� GetConnectPanel ��ȡ����������ݲ�ˢ�½ڵ��ͼ��
        /// </para>
        /// </summary>
        [RelayCommand]
        private async Task LoadPanelAsync(LoadPanelParameter loadParameter)
        {
            IsLoading = true;
            _viFilePath = loadParameter.ViFilePath;

            try
            {
                // ���� �׶�һ����� LabVIEW IDE �Ƿ����� ����������������������������������������������������
                bool ideConnected = await Task.Run(() => LvLibHelper.ConnectLabVIEW());

                LabViewIdeState = ideConnected ? LabViewIdeState.Connected : LabViewIdeState.Disconnected;
                LabViewIdeStateText = ideConnected ? "Connected" : "Disconnected";

                if (!ideConnected)
                {
                    // ���� IDE δ���ӣ���̨���� JSON��await ���� UI �̸߳��¼��� ����
                    // JSON �����ź�̨�̣߳�await ��ɺ��Զ��ص� UI �̣߳�
                    // ���� Dispatcher.Invoke������װ����� ThreadPool �̡߳�
                    var (controlList, indicatorList) = await Task.Run(() =>
                    {
                        var ctrl = new List<LvPanelNode>();
                        var ind = new List<LvPanelNode>();

                        try { ctrl = LvPanelConverter.ConvertFromJson(loadParameter.ControlJson); }
                        catch (Exception ex)
                        { Debug.WriteLine($"�� JSON ���� Control �ڵ�ʧ��: {ex.Message}"); }

                        try { ind = LvPanelConverter.ConvertFromJson(loadParameter.IndicatorJson); }
                        catch (Exception ex)
                        { Debug.WriteLine($"�� JSON ���� Indicator �ڵ�ʧ��: {ex.Message}"); }

                        return (ctrl, ind);
                    });

                    // await ���ѻص� UI �̣߳�ֱ�Ӳ��� ObservableCollection
                    ControlNodes.Clear();
                    foreach (var node in controlList)
                    {
                        ControlNodes.Add(node);
                        SubscribeNodePropertyChanged(node);
                    }

                    IndicatorNodes.Clear();
                    foreach (var node in indicatorList)
                    {
                        IndicatorNodes.Add(node);
                        SubscribeNodePropertyChanged(node);
                    }

                    Debug.WriteLine("LabVIEW IDE δ���ӣ��ѴӲ������ûָ��ڵ㡣");
                    return;
                }

                // ���� �׶ζ���IDE ���ߣ��� LabVIEW ��ȡ����������� ����������������������������
                var result = await Task.Run(() =>
                {
                    var panelInfo = LvLibHelper.GetConnectPanel(
                        viPath: loadParameter.ViFilePath,
                        controlIn: loadParameter.ControlJson,
                        indicatorIn: loadParameter.IndicatorJson);

                    var controlList = LvPanelConverter.ConvertFromJson(panelInfo.ControlOut);
                    var indicatorList = LvPanelConverter.ConvertFromJson(panelInfo.IndicatorOut);

                    return (controlList, indicatorList,
                            panelInfo.Width, panelInfo.Height,
                            panelInfo.PixelData,
                            panelInfo.IsReentrant, panelInfo.VIState);
                });

                // await ���ѻص� UI �̣߳�ֱ�Ӳ��� ObservableCollection
                ControlNodes.Clear();
                foreach (var node in result.controlList)
                {
                    ControlNodes.Add(node);
                    SubscribeNodePropertyChanged(node);
                }

                IndicatorNodes.Clear();
                foreach (var node in result.indicatorList)
                {
                    IndicatorNodes.Add(node);
                    SubscribeNodePropertyChanged(node);
                }

                // ���� UI �̣߳�ֱ�Ӵ��� WriteableBitmap������ Dispatcher.Invoke
                PanelImage = ConvertPixmapToImage(result.PixelData, result.Width, result.Height);
                IsReentrant = result.IsReentrant;
                ViState = result.VIState;
                ViStateText = GetVIStateText(result.VIState);

                UpdateControlJson();
                UpdateIndicatorJson();
            }
            catch (DllNotFoundException ex)
            {
                Debug.WriteLine($"�Ҳ��� lvLib.dll: {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                Debug.WriteLine($"DLL λ����ƥ��: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"����ʧ��: {ex.Message}");
                Debug.WriteLine($"��ջ����: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ���� ˽�и��� ��������������������������������������������������������������������������������������������������������������������

        private string GetVIStateText(VIState state) => state switch
        {
            VIState.Bad => "Bad",
            VIState.Idle => "Idle",
            VIState.RunTopLevel => "Run Top Level",
            VIState.Running => "Running",
            _ => "Unknown"
        };

        private void SubscribeNodePropertyChanged(LvPanelNode node)
        {
            node.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LvPanelNode.Value) ||
                    e.PropertyName == nameof(LvPanelNode.Variable) ||
                    e.PropertyName == nameof(LvPanelNode.ValueSourceType) ||
                    e.PropertyName == nameof(LvPanelNode.Log) ||
                    e.PropertyName == nameof(LvPanelNode.ActualValue))
                {
                    if (ControlNodes.Contains(node) || IsDescendantOf(node, ControlNodes))
                        UpdateControlJson();
                    else if (IndicatorNodes.Contains(node) || IsDescendantOf(node, IndicatorNodes))
                        UpdateIndicatorJson();
                }
            };

            foreach (var child in node.Children)
                SubscribeNodePropertyChanged(child);
        }

        private bool IsDescendantOf(LvPanelNode node, ObservableCollection<LvPanelNode> rootNodes)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (rootNodes.Contains(current)) return true;
                current = current.Parent;
            }
            return false;
        }

        private void UpdateControlJson()
        {
            try { ControlJson = LvPanelConverter.ConvertToJson(ControlNodes); }
            catch (Exception ex) { Debug.WriteLine($"���л� Control JSON ʧ��: {ex.Message}"); }
        }

        private void UpdateIndicatorJson()
        {
            try { IndicatorJson = LvPanelConverter.ConvertToJson(IndicatorNodes); }
            catch (Exception ex) { Debug.WriteLine($"���л� Indicator JSON ʧ��: {ex.Message}"); }
        }

        /// <summary>
        /// �� LabVIEW 24-bit pixmap ת��Ϊ WPF ImageSource��
        /// ���� UI �̵߳��ã�LoadPanelAsync �� await ֮�󣩣����� Dispatcher.Invoke��
        /// </summary>
        private ImageSource? ConvertPixmapToImage(uint[] pixelData, int width, int height)
        {
            if (pixelData == null || pixelData.Length == 0 || width <= 0 || height <= 0)
            {
                Debug.WriteLine("ͼ��������Ч");
                return null;
            }

            if (pixelData.Length != width * height)
            {
                Debug.WriteLine($"�������ݴ�С��ƥ��: ���� {width * height}, ʵ�� {pixelData.Length}");
                return null;
            }

            try
            {
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                bitmap.Lock();
                try
                {
                    unsafe
                    {
                        uint* pBackBuffer = (uint*)bitmap.BackBuffer;
                        int stride = bitmap.BackBufferStride / 4;

                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < width; x++)
                            {
                                uint pixel = pixelData[y * width + x];
                                // LabVIEW pixmap �� Alpha ʱ�� 0xFF
                                if ((pixel & 0xFF000000) == 0) pixel |= 0xFF000000;

                                uint a = (pixel >> 24) & 0xFF;
                                uint b = (pixel >> 16) & 0xFF;
                                uint g = (pixel >> 8) & 0xFF;
                                uint r = pixel & 0xFF;

                                // LabVIEW ARGB �� WPF Bgra32
                                pBackBuffer[y * stride + x] = (a << 24) | (b << 16) | (g << 8) | r;
                            }
                    }
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                }
                finally { bitmap.Unlock(); }

                var result = string.IsNullOrEmpty(_viFilePath)
                    ? bitmap
                    : AddTextAboveImage(bitmap, System.IO.Path.GetFileName(_viFilePath));

                result.Freeze();
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ͼ��ת��ʧ��: {ex.Message}");
                return null;
            }
        }

        private WriteableBitmap AddTextAboveImage(WriteableBitmap sourceBitmap, string text)
        {
            try
            {
                var measureVisual = new DrawingVisual();
                var typeface = new Typeface(
                    new FontFamily("Microsoft YaHei"),
                    FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, 14.0, Brushes.Black,
                    VisualTreeHelper.GetDpi(measureVisual).PixelsPerDip);

                const double padding = 8.0;
                double textHeight = formattedText.Height + padding * 2;
                int newWidth = sourceBitmap.PixelWidth;
                int newHeight = sourceBitmap.PixelHeight + (int)Math.Ceiling(textHeight);
                double textX = (newWidth - formattedText.Width) / 2.0;

                var visual = new DrawingVisual();
                using (var ctx = visual.RenderOpen())
                {
                    ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, newWidth, textHeight));
                    ctx.DrawText(formattedText, new Point(textX, padding));
                    ctx.DrawImage(sourceBitmap,
                        new Rect(0, textHeight, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight));
                }

                var renderTarget = new RenderTargetBitmap(
                    newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(visual);
                return new WriteableBitmap(renderTarget);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"�������ʧ��: {ex.Message}");
                return sourceBitmap;
            }
        }

        // ���� �ڵ�������� ������������������������������������������������������������������������������������������������������������

        [RelayCommand(CanExecute = nameof(CanRemoveNode))]
        private void RemoveNode(LvPanelNode? node)
        {
            if (node == null) return;
            try
            {
                if (node.Parent != null) node.Parent.Children.Remove(node);
                else { ControlNodes.Remove(node); IndicatorNodes.Remove(node); }
            }
            catch (Exception ex) { Debug.WriteLine($"�Ƴ��ڵ�ʧ��: {ex.Message}"); }
        }

        private bool CanRemoveNode(LvPanelNode? node) => node?.IsMissing == true;

        [RelayCommand]
        private void RemoveAllMissingNodes()
        {
            try
            {
                foreach (var node in GetMissingNodes(ControlNodes).ToList())
                    RemoveNodeRecursive(node, ControlNodes);
                foreach (var node in GetMissingNodes(IndicatorNodes).ToList())
                    RemoveNodeRecursive(node, IndicatorNodes);
            }
            catch (Exception ex) { Debug.WriteLine($"�����Ƴ���ʧ�ڵ�ʧ��: {ex.Message}"); }
        }

        private IEnumerable<LvPanelNode> GetMissingNodes(ObservableCollection<LvPanelNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsMissing) yield return node;
                foreach (var child in GetMissingNodes(node.Children))
                    yield return child;
            }
        }

        private void RemoveNodeRecursive(LvPanelNode node, ObservableCollection<LvPanelNode> rootNodes)
        {
            if (node.Parent != null) node.Parent.Children.Remove(node);
            else rootNodes.Remove(node);
        }
    }
}