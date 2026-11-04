using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Appearance;
using CAN.UI.Dbc;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using Syncfusion.Windows.Tools.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

/// <summary>
/// 新增页面真实 XAML 的同步呈现与保存/切换验证。全部 DBC 解析在创建
/// Syncfusion 控件前完成；不修改授权配置，不宣称宿主异步加载完整 E2E。
/// </summary>
internal static class BackgroundLayoutTests
{
    private sealed record Prepared(IStepPlugin Plugin, Step Step, object Model, string Name);

    internal static void VerifyActualPages()
    {
        var prepared = PreloadBeforeCreatingViews();
        // 创建控件后不运行 Dispatcher 消息循环、不 await；SDK 缺少授权时的
        // 延时授权模态窗口不能被本测试伪装成平台验证已完成。
        foreach (var entry in prepared)
        {
            if (entry.Model is DbcEditorViewModel dbc) VerifyDbc(entry, dbc);
            else VerifyRaw(entry, (CanBackgroundViewModel)entry.Model);
        }
    }

    private static void VerifyRaw(Prepared entry, CanBackgroundViewModel model)
    {
        var view = new CanBackgroundEditorView(model);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        Assert.Same(model, view.DataContext); Assert.Same(model, view.ViewModel);
        var tabs = Assert.IsType<TabControlExt>(view.Content);
        Layout(view);
        var taskEditor = (FrameworkElement)view.FindName("TaskNameEditor");
        Assert.Equal(0, tabs.SelectedIndex);
        AssertPresentation(view, taskEditor, true);
        var timeout = Assert.IsType<TextBox>(view.FindName("TimeoutBox"));
        Assert.Equal("5000", timeout.Text); Edit(timeout, "731");
        Assert.Equal(731, model.TimeoutMs);
        var result = Assert.IsType<TextBox>(view.FindName("ResultVariableBox"));
        AssertPresentation(view, result, model.HasResult);
        if (model.HasResult) Edit(result, "Locals.BackgroundJson");

        if (model.IsStart)
        {
            Assert.Equal("\"CanRx1\"", model.TaskName);
            Edit(Assert.IsType<TextBox>(view.FindName("PublishIntervalBox")), "37");
            Edit(Assert.IsType<TextBox>(view.FindName("MaxAgeBox")), "741");
            var grid = Assert.IsType<DataGrid>(view.FindName("MappingsGrid"));
            Assert.Equal(2, grid.Items.Count); Assert.True(CanListAppearance.GetEnabled(grid));
            var row = model.Mappings[0];
            var destination = grid.Columns.Single(column => Equals(column.Header, "最新十六进制数据 → 字符串变量"));
            grid.ScrollIntoView(row, destination); Layout(view);
            var target = Descendants(destination.GetCellContent(row)!).OfType<TextBox>().Single();
            Assert.Equal("Locals.RawA", target.Text); Edit(target, "Locals.LatestFrame");
            Assert.Equal("Locals.LatestFrame", row.DataVariable);
            Capture(view, entry.Name + ".png");
            Layout(view, 820); grid.ScrollIntoView(row, destination); Layout(view, 820);
            Assert.Contains(Descendants(grid).OfType<ScrollBar>(), bar => bar.Orientation == Orientation.Horizontal);
            Capture(view, entry.Name + "-narrow.png");
        }
        else if (model.IsStop)
        {
            var ignore = Assert.IsType<CheckBox>(view.FindName("IgnoreIfMissingCheckBox"));
            AssertPresentation(view, ignore, true); Assert.False(ignore.IsChecked);
            ignore.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            ignore.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
            Assert.True(model.IgnoreIfMissing); Capture(view, entry.Name + ".png");
        }
        else
        {
            var inactive = Assert.IsType<CheckBox>(view.FindName("IncludeInactiveCheckBox"));
            AssertPresentation(view, inactive, true); Assert.True(inactive.IsChecked);
            inactive.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
            inactive.GetBindingExpression(ToggleButton.IsCheckedProperty)!.UpdateSource();
            Assert.False(model.IncludeInactive); Capture(view, entry.Name + ".png");
        }
        model.Flush(); view.RefreshFromStep(entry.Step); Layout(view);
        Assert.Equal("731", timeout.Text);
        if (model.HasResult) Assert.Equal("Locals.BackgroundJson", result.Text);
        if (model.IsStart)
        {
            Assert.Equal(37, model.PublishIntervalMs); Assert.Equal(741, model.MaxAgeMs);
            Assert.Equal("Locals.LatestFrame", model.Mappings[0].DataVariable);
        }
        if (model.IsStop) Assert.True(model.IgnoreIfMissing);
        if (model.IsStatus) Assert.False(model.IncludeInactive);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    private static void VerifyDbc(Prepared entry, DbcEditorViewModel model)
    {
        var view = DbcStageQueue.Create(model);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        Assert.Same(model, view.ViewModel); Assert.Same(model, view.DataContext);
        Layout(view); var tabs = Assert.IsType<TabControlExt>(view.Content);
        Assert.True(model.IsBackgroundRead); Assert.Equal("\"DbcRx1\"", model.TaskName);
        AssertPresentation(view, (FrameworkElement)view.FindName("TaskNameEditor"), true);
        AssertPresentation(view, (FrameworkElement)view.FindName("PublishIntervalBox"), true);
        AssertPresentation(view, (FrameworkElement)view.FindName("CycleTimeBox"), false);
        var timeout = Assert.IsType<TextBox>(view.FindName("TimeoutBox")); Edit(timeout, "631");
        Edit(Assert.IsType<TextBox>(view.FindName("PublishIntervalBox")), "43");
        Edit(Assert.IsType<TextBox>(view.FindName("MaxAgeBox")), "643");
        var signals = Assert.IsType<DataGrid>(view.FindName("SignalsGrid"));
        Assert.Equal(5, signals.Items.Count);
        Assert.Contains(model.SignalRows, row => row.IsExtended && row.Name == "Wide");
        var selected = signals.Columns.Single(column => Equals(column.Header, "读取信号"));
        var targetColumn = signals.Columns.Single(column => Equals(column.Header, "输出变量（留空不输出）"));
        var voltage = model.SignalRows.Single(row => row.Name == "Voltage");
        signals.ScrollIntoView(voltage, targetColumn); Layout(view);
        var target = DbcRenderTests.EditOutput(signals, voltage, targetColumn, view);
        Assert.Equal("Locals.Voltage", target.Text); Edit(target, "Locals.LatestVoltage");
        var mode = model.SignalRows.Single(row => row.Name == "Mode");
        signals.ScrollIntoView(mode, selected); Layout(view);
        var checkbox = Descendants(selected.GetCellContent(mode)!).OfType<CheckBox>().Single();
        Assert.False(checkbox.IsChecked); Capture(view, entry.Name + ".png");
        model.Flush();
        var saved = (DbcReadBackgroundSetting)entry.Plugin.CreateSerializer().Deserialize(entry.Step.StepSetting.Setting, entry.Step.StepSetting.SettingVersion);
        Assert.Equal(631, saved.TimeoutMs); Assert.Equal(43, saved.PublishIntervalMs); Assert.Equal(643, saved.MaxAgeMs);
        Assert.Equal("Locals.LatestVoltage", saved.Messages[0].SignalOutputs.Single(row => row.Name == "Voltage").TargetVariable);
        tabs.SelectedIndex = 1; Layout(view);
        Assert.Contains("0x123", ((TextBlock)view.FindName("ProtectionOwnerText")).Text);
        Assert.Same(model.Protection, ((FrameworkElement)view.FindName("ProtectionEditor")).DataContext);
        Capture(view, entry.Name + "-protection.png");

        // 真实 RefreshFromStep 切换到无 DBC 的第二步骤，验证前一页保存与绑定隔离。
        // 同文档异步重载在 VM 测试覆盖；这里不在创建控件后泵送授权弹窗。
        var before = entry.Step.StepSetting.Setting.ToArray();
        var second = Step(entry.Plugin, new DbcReadBackgroundSetting { TaskName = "\"SecondRx\"", TimeoutMs = 902 });
        tabs.SelectedIndex = 0; view.RefreshFromStep(second); Layout(view);
        Assert.Equal("902", timeout.Text); Assert.Equal("\"SecondRx\"", model.TaskName);
        Assert.Empty(signals.Items); Assert.Equal(before, entry.Step.StepSetting.Setting);
        Edit(timeout, "903"); model.Flush();
        Assert.Equal(903, ((DbcReadBackgroundSetting)entry.Plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, second.StepSetting.SettingVersion)).TimeoutMs);
        Assert.Equal(before, entry.Step.StepSetting.Setting);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }

    private static List<Prepared> PreloadBeforeCreatingViews()
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var task = PrepareAsync();
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame(); bool expired = false;
                var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(20) };
                timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
                _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
                timer.Start(); try { Dispatcher.PushFrame(frame); } finally { timer.Stop(); }
                Assert.False(expired, "监听页模型预加载超过 20 秒，尚未创建实际控件");
            }
            return task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task<List<Prepared>> PrepareAsync()
    {
        var result = new List<Prepared>();
        (IStepPlugin Plugin, object Setting, string Name)[] raw =
        [
            (new CanTaskStatusPlugin(), new CanTaskStatusSetting { ResultVariable = "Locals.Tasks" }, "can-task-status"),
            (new CanReadBackgroundStartPlugin(), new CanReadBackgroundSetting { ResultVariable = "Locals.CanLatest", Mappings =
                [new() { CanId = "\"0x123\"", DataVariable = "Locals.RawA" }, new() { CanId = "\"0x456\"", FrameType = CanFrameType.Extended, FrameFormat = CanReceiveFrameFormat.Fd, DataVariable = "Locals.RawB" }] }, "can-listen-start"),
            (new CanReadBackgroundStopPlugin(), new CanReadBackgroundStopSetting(), "can-listen-stop"),
            (new DbcReadBackgroundStopPlugin(), new DbcReadBackgroundStopSetting(), "dbc-listen-stop")
        ];
        foreach (var (plugin, setting, name) in raw)
        {
            var step = Step(plugin, setting); var model = new CanBackgroundViewModel(plugin); model.Attach(step);
            result.Add(new(plugin, step, model, name));
        }
        var dbcPlugin = new DbcReadBackgroundStartPlugin();
        var dbcStep = Step(dbcPlugin, new DbcReadBackgroundSetting
        {
            Document = new() { Name = "listener-layout.dbc", Text = DbcEditorTests.DocumentText },
            ResultVariable = "Locals.LatestSignals", UseMessageList = true,
            Messages = [new() { MessageId = 0x123, SignalOutputs =
                [new() { Name = "Voltage", TargetVariable = "Locals.Voltage" }, new() { Name = "Mode", Selected = false }] },
                new() { MessageId = 0x123, FrameType = CanFrameType.Extended }]
        });
        var dbcModel = new DbcEditorViewModel(dbcPlugin); dbcModel.Attach(dbcStep); await dbcModel.LoadTask;
        Assert.False(dbcModel.IsLoading); Assert.Empty(dbcModel.LoadError);
        result.Add(new(dbcPlugin, dbcStep, dbcModel, "dbc-listen-start")); return result;
    }

    private static Step Step(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion; return step;
    }
    private static void Edit(TextBox box, string text)
    { box.SetCurrentValue(TextBox.TextProperty, text); box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); }
    private static void Layout(FrameworkElement view, double width = 1500)
    { DbcStageQueue.LayoutAndFinish(view, new Size(width, 1080)); }
    private static void AssertPresentation(FrameworkElement root, FrameworkElement element, bool expected)
    {
        // 此 fixture 没有 Window/HwndSource；WPF IsVisible 会连同离屏根返回 false。
        // 实际呈现断言检查绑定的 Visibility、完整视觉祖先链及布局尺寸，
        // 仍能发现未选择页面、隐藏祖先或未实例化/未布局的内容。
        var ancestors = new List<DependencyObject>();
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            ancestors.Add(current);
        bool hidden = ancestors.OfType<UIElement>().Any(item => item.Visibility != Visibility.Visible);
        Assert.Equal(!expected, hidden);
        if (!expected) return;
        Assert.Contains(root, ancestors);
        Assert.True(element.ActualWidth > 0 && element.ActualHeight > 0,
            $"{element.Name} 未布局：{element.ActualWidth}x{element.ActualHeight}");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        yield return parent;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(parent, i))) yield return child;
    }
    private static void Capture(FrameworkElement view, string name)
    {
        var output = Environment.GetEnvironmentVariable("CAN_EDITOR_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
    }
}
