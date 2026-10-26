using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Syncfusion.Windows.Tools.Controls;
using CAN.CANopen.Cyclic;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

/// <summary>六种独立步骤共用布局组件，操作固定，编辑字段按当前步骤展示。</summary>
public sealed class CyclicServiceEditorView : UserControl, IRefreshableEditor
{
    public CyclicServiceViewModel ViewModel { get; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CyclicServiceEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CyclicServiceEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    private readonly StackPanel _panel = new() { Margin = new Thickness(16) };
    public CyclicServiceEditorView(CyclicPluginBase plugin)
    {
        ViewModel = new(plugin);
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.Flush();
        NameScope.SetNameScope(this, new NameScope());
        var tabs = new TabControlExt { CloseButtonType = CloseButtonType.Hide, AllowDragDrop = false, EnableLabelEdit = false };
        tabs.Items.Add(new TabItemExt { Header = plugin.DisplayName, Image = new BitmapImage(new Uri(plugin.IconPath)),
            ImageHeight = 20, ImageWidth = 20, Content = new ScrollViewer { Content = _panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        Content = tabs;
        _panel.Children.Add(new TextBlock { Text = plugin.DisplayName, FontSize = 16, Margin = new Thickness(0, 0, 0, 12) });
        AddText("连接名（表达式）", "ConnectionName");
        AddText("服务名（表达式）", "ServiceName");
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12), Text =
            "仅管理通信，不修改 PDO 映射。CycleStop 不代表电机已停稳；停机命令必须来自设备审查配置。时间质量未知时 Valid=false。软件周期不是硬实时。" };
        RegisterName("ServiceHint", hint);
        _panel.Children.Add(hint);
        if (plugin.Operation == CyclicOperation.ServiceStart)
        {
            AddGrid(true);
            AddCheck("显式启用监测 SYNC（关闭时保持原有仅接收语义）", "EnableMonitoringSync");
            AddText("监测 SYNC 周期（ms）", "MonitoringPeriodMs");
            AddText("SYNC COB-ID（表达式，本工程 0x80）", "SyncCobId");
            AddText("SYNC 数据（表达式，本工程填 \"00\"，DLC=1）", "SyncData");
            AddText("发送确认期限（ms）", "CycleTimeoutMs");
            AddText("最大起始迟到（ms）", "MaxLatenessMs");
            AddText("启动失败收尾总期限（ms）", "StopTimeoutMs");
            var timingHint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8), Text =
                "纯监测以发送确认期限判断传输超时；批次耗时略大于监测周期仅记录诊断。最大起始迟到单独校验。监测间隔从上一批发送完成后计算，快照 Timing 可查看实际配置及主机/适配器分段耗时。" };
            RegisterName("MonitoringTimingHint", timingHint);
            _panel.Children.Add(timingHint);
        }
        if (plugin.Operation is CyclicOperation.CycleStart or CyclicOperation.CycleInputsUpdate) AddGrid(false);
        if (plugin.Operation == CyclicOperation.CycleStart)
        {
            AddText("节点列表（字符串表达式，如 \"1,2\"）", "NodeIds");
            AddText("SYNC COB-ID（表达式）", "SyncCobId");
            AddText("SYNC 数据（表达式，空字符串表示 DLC=0）", "SyncData");
            AddText("周期（ms）", "PeriodMs");
            AddText("单周期期限（ms）", "CycleTimeoutMs");
            AddText("最大起始迟到（ms）", "MaxLatenessMs");
            AddText("目标输入有效期（ms）", "InputMaxAgeMs");
            AddCheck("监督反馈有效性（过期/未知时停止目标）", "SuperviseFeedback");
            AddText("反馈启动宽限（ms）", "FeedbackStartupGraceMs");
            AddCheck("执行有限不可变点表（不要求实时输入续期）", "UseFiniteTrajectory");
            AddText("点表总持续时间（ms）", "TrajectoryDurationMs");
            AddText("点表 JSON 字符串表达式（OffsetMs/Frames/CobId/DataHex）", "TrajectoryJson");
            AddText("所有者停机 SDO JSON 字符串表达式（NodeId/Index/SubIndex/DataHex）", "StopCommandsJson");
            AddText("全部停机命令总期限（ms）", "StopTimeoutMs");
            _panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "JSON ID/索引使用十进制整数，DataHex 保持原始小端字节。示例见 CAN/CONTROLLED_CANOPEN.md。监测 SYNC 下启动目标必须先配置停机命令；SDO ACK 仅确认协议成功，不证明电机停稳。" });
            AddCheck("要求实际发送确认（默认开启；厂商能力不足时拒绝启动）", "RequireOnBusConfirmation");
            _panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "取消确认仅用于主机提交顺序验证，不证明物理 RPDO→SYNC 顺序。待发帧状态未知时停止仍报错并保持占用。软件周期不是硬实时。" });
        }
        if (plugin.Operation == CyclicOperation.SnapshotRead)
        {
            AddText("快照 JSON 输出（已声明 string 变量路径）", "ResultVariable");
            AddCheck("要求所有反馈可信且未过期", "RequireAllValid");
        }
        if (plugin.Operation is CyclicOperation.CycleStop or CyclicOperation.ServiceStop)
            AddText("退出确认期限（ms）", "StopTimeoutMs");
        if (plugin.Operation == CyclicOperation.ServiceStop)
        {
            var stopHint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8), Text =
                "完成仅表示服务、订阅及控制占用真实退出，发送已确认且无部分周期。历史故障仍在结果 JSON 和警告中；不伪造停机命令 ACK 或电机停稳。实际关闭失败、未知待发或退出超时仍报错并保留占用。" };
            RegisterName("ServiceStopResultHint", stopHint);
            _panel.Children.Add(stopHint);
        }
    }

    private static Binding Bind(string property) => new(property)
    { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnExceptions = true };
    private void AddText(string label, string property)
    {
        _panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3), TextWrapping = TextWrapping.Wrap });
        FrameworkElement editor;
        if (property is "ConnectionName" or "ServiceName" or "NodeIds" or "SyncCobId" or "SyncData" or "TrajectoryJson" or "StopCommandsJson")
            editor = (FrameworkElement)XamlReader.Parse(ExpressionMarkup(property, property == "SyncCobId" ? "Double" : "String"));
        else
        {
            var text = new TextBox { MinWidth = 100 };
            text.SetBinding(TextBox.TextProperty, Bind(property));
            editor = text;
        }
        RegisterName(property + "Editor", editor);
        _panel.Children.Add(editor);
    }
    private static string ExpressionMarkup(string property, string expected) =>
        $"<expr:ExpressionTextBox xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:expr='clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox' MinWidth='100' ExpectedResultType='{expected}' ScriptText='{{Binding {property}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}}' SequenceFile='{{Binding SequenceFile, RelativeSource={{RelativeSource AncestorType=UserControl}}}}' EditPosition='{{Binding EditPosition, RelativeSource={{RelativeSource AncestorType=UserControl}}}}' />";
    private static DataGridTemplateColumn ExpressionColumn(string header, string property, string expected, DataGridLength width)
    {
        var template = (DataTemplate)XamlReader.Parse($"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>{ExpressionMarkup(property, expected)}</DataTemplate>");
        return new DataGridTemplateColumn { Header = header, CellTemplate = template, CellEditingTemplate = template, Width = width };
    }
    private void AddCheck(string label, string property)
    {
        var check = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 8, 0, 8) };
        check.SetBinding(CheckBox.IsCheckedProperty, Bind(property));
        _panel.Children.Add(check);
    }
    private void AddGrid(bool feedback)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            CanUserSortColumns = false, CanUserReorderColumns = false, Height = 200, Margin = new Thickness(0, 8, 0, 4) };
        if (feedback) grid.Columns.Add(new DataGridTextColumn { Header = "名称", Binding = Bind("Name"), Width = 100 });
        grid.Columns.Add(ExpressionColumn("COB-ID 表达式", "CobId", "Double", 180));
        grid.Columns.Add(new DataGridTextColumn { Header = "DLC", Binding = Bind("Dlc"), Width = 55 });
        if (feedback) grid.Columns.Add(new DataGridTextColumn { Header = "有效期 ms", Binding = Bind("MaxAgeMs"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        else grid.Columns.Add(ExpressionColumn("原始数据表达式（保持组包）", "Data", "String", new DataGridLength(1, DataGridLengthUnitType.Star)));
        grid.ItemsSource = feedback ? ViewModel.Routes : ViewModel.Targets;
        RegisterName(feedback ? "RoutesGrid" : "TargetsGrid", grid);
        _panel.Children.Add(grid);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var add = new Button { Content = "添加", Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 3, 12, 3) };
        add.Click += (_, _) => { if (feedback) ViewModel.AddRoute(); else ViewModel.AddTarget(); };
        var remove = new Button { Content = "删除选中", Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 3, 12, 3) };
        remove.Click += (_, _) =>
        {
            if (grid.SelectedItem is not CyclicRowViewModel row) return;
            if (feedback) ViewModel.RemoveRoute(row); else ViewModel.RemoveTarget(row);
        };
        buttons.Children.Add(add); buttons.Children.Add(remove); _panel.Children.Add(buttons);
    }
    public void RefreshFromStep(Step step) => ViewModel.Load(step);
}
