using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExperssionEditor;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI.ViewModels
{
    /// <summary>
    /// LabVIEW 面板视图模型
    /// </summary>
    public partial class LvPanelViewModel : ObservableObject
    {
        /// <summary>加载面板参数</summary>
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

        /// <summary>VI 当前运行状态</summary>
        [ObservableProperty] private VIState _viState;

        /// <summary>VI 状态显示文本</summary>
        [ObservableProperty] private string _viStateText;

        /// <summary>LabVIEW IDE 连接状态</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLabViewDisconnected))]
        private LabViewIdeState _labViewIdeState;

        /// <summary>LabVIEW IDE 未连接时为 true（用于面板提示层可见性绑定）</summary>
        public bool IsLabViewDisconnected =>
            LabViewIdeState == LabViewIdeState.Disconnected ||
            LabViewIdeState == LabViewIdeState.Unknown;

        /// <summary>LabVIEW IDE 状态显示文本</summary>
        [ObservableProperty] private string _labViewIdeStateText;

        // 明确标注可空，避免 NullReferenceException
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

        // ── 命令 ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private void OpenVariableView(LvPanelNode? node)
        {
            // SequenceFile 未赋值时直接忽略，避免 NullReferenceException
            if (SequenceFile is null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new ExpressionEditorDialog(
                    SequenceFile,
                    EditPosition,
                    node?.Variable ?? string.Empty,
                    node?.Type ?? string.Empty)
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
        /// 加载 VI 面板连接信息。
        /// <para>
        /// 阶段一：检查 LabVIEW IDE 是否在线。<br/>
        /// IDE 未连接 → 后台解析步骤设置 JSON 恢复节点，await 后在 UI 线程填充集合，跳过 DLL 调用。<br/>
        /// IDE 已连接 → 调用 GetConnectPanel 获取最新面板数据并刷新节点和图像。
        /// </para>
        /// </summary>
        [RelayCommand]
        private async Task LoadPanelAsync(LoadPanelParameter loadParameter)
        {
            IsLoading = true;
            _viFilePath = loadParameter.ViFilePath;

            try
            {
                // ── 阶段一：检查 LabVIEW IDE 是否在线 ──────────────────────────
                bool ideConnected = await Task.Run(() => LvLibHelper.ConnectLabVIEW());

                LabViewIdeState = ideConnected ? LabViewIdeState.Connected : LabViewIdeState.Disconnected;
                LabViewIdeStateText = ideConnected ? "Connected" : "Disconnected";

                if (!ideConnected)
                {
                    // ── IDE 未连接：后台解析 JSON，await 后在 UI 线程更新集合 ──
                    // JSON 解析放后台线程；await 完成后自动回到 UI 线程，
                    // 无需 Dispatcher.Invoke，避免白白阻塞 ThreadPool 线程。
                    var (controlList, indicatorList) = await Task.Run(() =>
                    {
                        var ctrl = new List<LvPanelNode>();
                        var ind = new List<LvPanelNode>();

                        try { ctrl = LvPanelConverter.ConvertFromJson(loadParameter.ControlJson); }
                        catch (Exception ex)
                        { Debug.WriteLine($"从 JSON 解析 Control 节点失败: {ex.Message}"); }

                        try { ind = LvPanelConverter.ConvertFromJson(loadParameter.IndicatorJson); }
                        catch (Exception ex)
                        { Debug.WriteLine($"从 JSON 解析 Indicator 节点失败: {ex.Message}"); }

                        return (ctrl, ind);
                    });

                    // await 后已回到 UI 线程，直接操作 ObservableCollection
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

                    Debug.WriteLine("LabVIEW IDE 未连接，已从步骤设置恢复节点。");
                    return;
                }

                // ── 阶段二：IDE 在线，从 LabVIEW 获取最新面板数据 ──────────────
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

                // await 后已回到 UI 线程，直接操作 ObservableCollection
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

                // 已在 UI 线程，直接创建 WriteableBitmap，无需 Dispatcher.Invoke
                PanelImage = ConvertPixmapToImage(result.PixelData, result.Width, result.Height);
                IsReentrant = result.IsReentrant;
                ViState = result.VIState;
                ViStateText = GetVIStateText(result.VIState);

                UpdateControlJson();
                UpdateIndicatorJson();
            }
            catch (DllNotFoundException ex)
            {
                Debug.WriteLine($"找不到 lvLib.dll: {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                Debug.WriteLine($"DLL 位数不匹配: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载失败: {ex.Message}");
                Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────────

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
            catch (Exception ex) { Debug.WriteLine($"序列化 Control JSON 失败: {ex.Message}"); }
        }

        private void UpdateIndicatorJson()
        {
            try { IndicatorJson = LvPanelConverter.ConvertToJson(IndicatorNodes); }
            catch (Exception ex) { Debug.WriteLine($"序列化 Indicator JSON 失败: {ex.Message}"); }
        }

        /// <summary>
        /// 将 LabVIEW 24-bit pixmap 转换为 WPF ImageSource。
        /// 仅在 UI 线程调用（LoadPanelAsync 的 await 之后），无需 Dispatcher.Invoke。
        /// </summary>
        private ImageSource? ConvertPixmapToImage(uint[] pixelData, int width, int height)
        {
            if (pixelData == null || pixelData.Length == 0 || width <= 0 || height <= 0)
            {
                Debug.WriteLine("图像数据无效");
                return null;
            }

            if (pixelData.Length != width * height)
            {
                Debug.WriteLine($"像素数据大小不匹配: 期望 {width * height}, 实际 {pixelData.Length}");
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
                                // LabVIEW pixmap 无 Alpha 时补 0xFF
                                if ((pixel & 0xFF000000) == 0) pixel |= 0xFF000000;

                                uint a = (pixel >> 24) & 0xFF;
                                uint b = (pixel >> 16) & 0xFF;
                                uint g = (pixel >> 8) & 0xFF;
                                uint r = pixel & 0xFF;

                                // LabVIEW ARGB → WPF Bgra32
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
                Debug.WriteLine($"图像转换失败: {ex.Message}");
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
                Debug.WriteLine($"添加文字失败: {ex.Message}");
                return sourceBitmap;
            }
        }

        // ── 节点操作命令 ──────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRemoveNode))]
        private void RemoveNode(LvPanelNode? node)
        {
            if (node == null) return;
            try
            {
                if (node.Parent != null) node.Parent.Children.Remove(node);
                else { ControlNodes.Remove(node); IndicatorNodes.Remove(node); }
            }
            catch (Exception ex) { Debug.WriteLine($"移除节点失败: {ex.Message}"); }
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
            catch (Exception ex) { Debug.WriteLine($"批量移除丢失节点失败: {ex.Message}"); }
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