using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpressionEditor;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Helper;
using LabVIEWCallPlugin.UI.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI.ViewModels;

public partial class LvPanelViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<LvPanelNode> _controlNodes = [];
    [ObservableProperty] private ObservableCollection<LvPanelNode> _indicatorNodes = [];
    [ObservableProperty] private LvPanelNode? _selectedNode;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowConnectionHint))] private ImageSource? _panelImage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ReentrantStateText))] private bool _isReentrant;
    [ObservableProperty] private VIState _viState = VIState.Idle;
    [ObservableProperty] private string _viStateText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsLabViewDisconnected)),
        NotifyPropertyChangedFor(nameof(ShowConnectionHint)), NotifyPropertyChangedFor(nameof(ConnectionStatusTitle)),
        NotifyPropertyChangedFor(nameof(ReentrantStateText))]
    private LabViewIdeState _labViewIdeState;
    [ObservableProperty] private string _labViewIdeStateText = "";
    [ObservableProperty] private SequenceFile? _sequenceFile;
    [ObservableProperty] private EditPosition? _editPosition;

    public bool IsLabViewDisconnected => LabViewIdeState == LabViewIdeState.Disconnected;
    public string ReentrantStateText => LabViewIdeState == LabViewIdeState.Connected
        ? (IsReentrant ? "Yes" : "No") : "未确认";
    public bool ShowConnectionHint => LabViewIdeState != LabViewIdeState.Connected || PanelImage == null;
    public string ConnectionStatusTitle => LabViewIdeState switch
    {
        LabViewIdeState.Connected => "已连接，暂无面板预览",
        LabViewIdeState.Disconnected => "在线检查失败",
        _ => "尚未在线检查"
    };
    public bool HasUnsupportedLogs => AllNodes(ControlNodes.Concat(IndicatorNodes)).Any(node => node.Log);
    public event Action<bool, string>? BindingsChanged;

    public void ShowSnapshot(List<LvPanelNode> inputs, List<LvPanelNode> outputs, ConnectPanelInfo? online)
    {
        var preview = online == null || online.Width == 0 || online.Height == 0 ? null : CreatePreview(online);
        foreach (var node in AllNodes(ControlNodes)) node.PropertyChanged -= InputChanged;
        foreach (var node in AllNodes(IndicatorNodes)) node.PropertyChanged -= OutputChanged;
        ControlNodes = new(inputs);
        IndicatorNodes = new(outputs);
        foreach (var node in AllNodes(inputs)) node.PropertyChanged += InputChanged;
        foreach (var node in AllNodes(outputs)) node.PropertyChanged += OutputChanged;
        IsReentrant = online?.IsReentrant ?? false;
        ViState = online?.VIState ?? VIState.Idle;
        ViStateText = online?.VIState.ToString() ?? "尚未在线检查";
        LabViewIdeState = online == null ? LabViewIdeState.Unknown : LabViewIdeState.Connected;
        LabViewIdeStateText = online == null ? "当前仅显示已保存端子，未检查 LabVIEW 连接。安装并打开 LabVIEW 开发环境后，点击刷新进行在线核对。" : "本次在线检查成功；状态不会自动持续刷新。";
        PanelImage = preview;
        OnPropertyChanged(nameof(HasUnsupportedLogs));
    }

    public void MarkNotChecked(string message)
    {
        LabViewIdeState = LabViewIdeState.Unknown;
        LabViewIdeStateText = message;
        ViStateText = "尚未在线检查";
        IsReentrant = false;
        PanelImage = null;
    }

    public void MarkOnlineFailure(string message)
    {
        LabViewIdeState = LabViewIdeState.Disconnected;
        LabViewIdeStateText = message;
        ViStateText = "本次检查失败";
        IsReentrant = false;
        PanelImage = null;
    }

    private static ImageSource CreatePreview(ConnectPanelInfo panel)
    {
        if (panel.PixelData.Length != checked(panel.Width * panel.Height)) throw new InvalidDataException("VI 面板像素长度不匹配。");
        var pixels = new int[panel.PixelData.Length];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = unchecked((int)(panel.PixelData[i] | 0xff000000));
        var image = BitmapSource.Create(panel.Width, panel.Height, 96, 96, PixelFormats.Bgra32, null,
            pixels, checked(panel.Width * sizeof(int)));
        image.Freeze();
        return image;
    }

    private static IEnumerable<LvPanelNode> AllNodes(IEnumerable<LvPanelNode> roots)
    {
        foreach (var node in roots)
        {
            yield return node;
            foreach (var child in AllNodes(node.Children)) yield return child;
        }
    }

    private static bool IsEditable(string? name) => name is
        nameof(LvPanelNode.Value) or nameof(LvPanelNode.ValueSourceType) or nameof(LvPanelNode.Variable) or nameof(LvPanelNode.Log);
    private void InputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsEditable(e.PropertyName)) BindingsChanged?.Invoke(false, LvPanelConverter.ConvertToJson(ControlNodes));
    }
    private void OutputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsEditable(e.PropertyName)) BindingsChanged?.Invoke(true, LvPanelConverter.ConvertToJson(IndicatorNodes));
    }

    [RelayCommand]
    private void ClearUnsupportedLogs()
    {
        Clear(ControlNodes, false);
        Clear(IndicatorNodes, true);
        OnPropertyChanged(nameof(HasUnsupportedLogs));

        void Clear(ObservableCollection<LvPanelNode> roots, bool output)
        {
            var flagged = AllNodes(roots).Where(node => node.Log).ToArray();
            if (flagged.Length == 0) return;
            PropertyChangedEventHandler changed = output ? OutputChanged : InputChanged;
            foreach (var node in flagged)
            {
                node.PropertyChanged -= changed;
                try { node.Log = false; }
                finally { node.PropertyChanged += changed; }
            }
            BindingsChanged?.Invoke(output, LvPanelConverter.ConvertToJson(roots));
        }
    }

    [RelayCommand]
    private void OpenVariableView(LvPanelNode? node)
    {
        if (SequenceFile == null || EditPosition == null || node == null) return;
        var dialog = new ExpressionEditorDialog(SequenceFile, EditPosition, node.Variable,
            LabVIEWTypeConverter.ConvertToString(node.Type)) { Owner = Application.Current?.MainWindow };
        if (dialog.ShowDialog() == true)
        {
            node.Variable = dialog.ScriptText;
            if (AllNodes(ControlNodes).Contains(node)) node.ValueSourceTypeEnum = ValueSourceType.Variable;
        }
    }

    [RelayCommand]
    private void RemoveNode(LvPanelNode? node)
    {
        if (node?.IsMissing != true) return;
        var input = AllNodes(ControlNodes).Contains(node);
        if (!input && !AllNodes(IndicatorNodes).Contains(node)) return;
        foreach (var item in AllNodes([node]))
        {
            item.PropertyChanged -= InputChanged;
            item.PropertyChanged -= OutputChanged;
        }
        (node.Parent?.Children ?? (input ? ControlNodes : IndicatorNodes)).Remove(node);
        BindingsChanged?.Invoke(!input, LvPanelConverter.ConvertToJson(input ? ControlNodes : IndicatorNodes));
        OnPropertyChanged(nameof(HasUnsupportedLogs));
    }

    [RelayCommand]
    private void RemoveAllMissingNodes()
    {
        foreach (var node in AllNodes(ControlNodes.Concat(IndicatorNodes)).Where(n => n.IsMissing).ToArray())
            RemoveNode(node);
    }
}
