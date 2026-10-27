using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Syncfusion.Windows.Tools.Controls;
using CAN.CANopen.Cyclic;
using CAN.Runtime;
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
            "仅管理通信，不修改 PDO 映射。默认整批仅提交，不等发送回显；读取与发送独立。CycleStop 不代表电机已停稳；可选停机命令来自设备审查配置。时间质量未知时 Valid=false，软件周期不是硬实时。适配器能力见 Timing.Batch.Strategy。" };
        RegisterName("ServiceHint", hint);
        _panel.Children.Add(hint);
        if (plugin.Operation == CyclicOperation.ServiceStart)
        {
            AddGrid(true);
            AddCheck("启用本服务的周期 SYNC（可创建多个独立发送源）", "EnableMonitoringSync");
            AddText("本发送源 SYNC 独立周期（ms）", "MonitoringPeriodMs");
            AddText("背景 SYNC COB-ID（表达式，本工程 0x80）", "SyncCobId");
            AddText("背景 SYNC 数据（表达式，本工程填 \"00\"，DLC=1）", "SyncData");
            AddCheck("独占诊断：逐批等待发送确认（正常周期默认关闭）", "RequireOnBusConfirmation");
            AddText("发送提交/确认总期限（ms）", "CycleTimeoutMs");
            AddText("最大起始迟到（ms）", "MaxLatenessMs");
            AddCheck("仅监测迟到时跳过该拍并重同步（默认开启，运动仍严格）", "ResynchronizeLateMonitoring");
            AddText("仅监测连续迟到故障次数（默认3）", "MonitoringConsecutiveLatenessLimit");
            AddText("启动失败收尾总期限（ms）", "StopTimeoutMs");
            var timingHint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8), Text =
                "每个服务有独立的SYNC配置、周期和生命周期，可不配置反馈。正常周期仅提交、不等回显；需要返回值请使用独立读取/SDO步骤。RPDO后可额外发SYNC，不重置背景周期、不合并其它源。共享连接按完整批次排队；批内不插帧。提交期限仍约束排队/SDK调用，发送确认期限只适用于显式诊断。停发不等于电机停稳，Windows周期不是硬实时。" };
            RegisterName("MonitoringTimingHint", timingHint);
            _panel.Children.Add(timingHint);
        }
        if (plugin.Operation is CyclicOperation.CycleStart or CyclicOperation.CycleInputsUpdate) AddGrid(false);
        if (plugin.Operation == CyclicOperation.CycleStart)
        {
            AddText("节点列表（字符串表达式，如 \"1,2\"）", "NodeIds");
            AddText("指令 SYNC COB-ID（表达式，与背景独立设置）", "SyncCobId");
            AddText("指令 SYNC 数据（表达式，空字符串表示 DLC=0）", "SyncData");
            AddText("目标 RPDO＋额外 SYNC 周期（ms，独立于背景 SYNC）", "PeriodMs");
            AddText("单周期期限（ms）", "CycleTimeoutMs");
            AddText("最大起始迟到（ms）", "MaxLatenessMs");
            AddText("目标输入有效期（ms）", "InputMaxAgeMs");
            AddCheck("监督反馈有效性（过期/未知时停止目标）", "SuperviseFeedback");
            AddText("反馈启动宽限（ms）", "FeedbackStartupGraceMs");
            AddCheck("执行有限不可变点表（不要求实时输入续期）", "UseFiniteTrajectory");
            AddText("点表总持续时间（ms）", "TrajectoryDurationMs");
            AddText("点表 JSON 字符串表达式（OffsetMs/Frames/CobId/DataHex）", "TrajectoryJson");
            var policy = new ComboBox { ItemsSource = new[]
            {
                new { Value = TrajectoryMissPolicy.Fail, Text = "立即失败（Fail，保留旧严格行为）" },
                new { Value = TrajectoryMissPolicy.SkipAndCatchUp, Text = "只发最新到期点（SkipAndCatchUp）" },
                new { Value = TrajectoryMissPolicy.HoldLast, Text = "有限保持上次原始目标（HoldLast）" }
            }, SelectedValuePath = "Value", DisplayMemberPath = "Text", MinWidth = 200 };
            policy.SetBinding(ComboBox.SelectedValueProperty, Bind("MissPolicy")); RegisterName("MissPolicyEditor", policy);
            _panel.Children.Add(new TextBlock { Text = "有限点表错点策略", Margin = new Thickness(0, 8, 0, 3) }); _panel.Children.Add(policy);
            AddText("点表迟到窗口（ms，0使用目标周期）", "LatenessWindowMs");
            AddText("累计允许跳点上限（超出则失败）", "MaxSkippedPoints");
            AddText("HoldLast最长保持（ms，不延长轨迹总时长）", "HoldTimeoutMs");
            _panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "点表策略只选择原始字节，不校验电机速度/加速度连续性；跳点会损失轨迹精度。初点不能跳过，终点须真实提交且不在总时长外强发。HoldLast不等于物理位置保持，必须配置停机命令。" });
            AddText("所有者停机 SDO JSON 字符串表达式（NodeId/Index/SubIndex/DataHex）", "StopCommandsJson");
            AddText("全部停机命令总期限（ms）", "StopTimeoutMs");
            _panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "JSON ID/索引使用十进制整数，DataHex保持原始字节。多个SYNC源共享ID；周期源间RPDO冲突拒绝，普通CAN/CANopen/CiA402补充操作仍可排队。队列项是整批RPDO→指令SYNC，不能按单帧拆排。可选停机SDO等待协议应答，ACK不证明物理停稳。" });
            AddCheck("独占诊断：逐批等待发送确认（正常周期默认关闭）", "RequireOnBusConfirmation");
            _panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text =
                "允许共享同一连接的补充收发；正常仅提交模式的停止只证明本源任务及原生调用退出，不要求回显。真实SDK失败、部分提交或调用未退出仍返回错误。读取与发送独立，共用接收路由，不另开硬件读者。" });
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
                "完成表示本服务任务、原生提交调用、订阅及占用已退出；不要求仅提交模式收到回显。其它发送源与读取继续运行。历史故障保留，不伪造发送确认或电机停稳；真正的部分提交/调用未退出仍报错。" };
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
        RegisterName(property + "Editor", check);
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
