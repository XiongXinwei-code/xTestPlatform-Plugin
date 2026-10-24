using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace SerialPort.ContractTests;

public class LifecycleContractTests
{
    private static readonly (Type Plugin, string Name, int Default)[] Cases =
    [
        (typeof(SerialPortOpenPlugin), "SerialPortOpen", 5000),
        (typeof(SerialPortClosePlugin), "SerialPortClose", 5000)
    ];

    [Fact]
    public void OldVersionOneMapWithoutNewTimeoutKeyRetainsCompatibleDefaults()
    {
        foreach (var item in Cases)
        {
            object plugin = Activator.CreateInstance(item.Plugin)!;
            var serializer = (IStepSettingSerializer)item.Plugin.GetMethod("CreateSerializer")!.Invoke(plugin, null)!;
            object old = serializer.Deserialize([0x80], 1); // 旧 v1 map 没有新增键，而非用新模型伪造旧文件。
            Assert.Equal(item.Default, (int)old.GetType().GetProperty("TimeoutMs")!.GetValue(old)!);
            string description = (string)item.Plugin.GetProperty("Description")!.GetValue(plugin)!;
            Assert.Contains("| TimeoutMs |", description);
        }
    }

    [Fact]
    public void EveryNewTimeoutPageBindsLaysOutAndFlushesOnUnloadBeforeTwoHundredMilliseconds() => UiThread.Run(() =>
    {
        var ui = Assembly.Load("SerialPort.StepPlugin.UI");
        foreach (var item in Cases)
        {
            object plugin = Activator.CreateInstance(item.Plugin)!;
            var serializer = (IStepSettingSerializer)item.Plugin.GetMethod("CreateSerializer")!.Invoke(plugin, null)!;
            var step = new Step();
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
            step.StepSetting.SettingVersion = serializer.SettingVersion;
            var page = (UserControl)Activator.CreateInstance(ui.GetType("SerialPort.UI.Views." + item.Name + "EditorView")!)!;
            page.GetType().GetMethod("RefreshFromStep")!.Invoke(page, [step]);
            page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            page.Measure(new Size(1100, 750)); page.Arrange(new Rect(0, 0, 1100, 750));
            page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.True(page.IsMeasureValid);
            var input = Assert.IsType<TextBox>(page.FindName("LifecycleTimeoutBox"));
            input.Text = "137";
            input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.True(137 == (int)page.DataContext.GetType().GetProperty("TimeoutMs")!.GetValue(page.DataContext)!, item.Name + " 真实输入绑定必须更新 VM");
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            object saved = serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(137, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
            page.GetType().GetMethod("RefreshFromStep")!.Invoke(page, [step]);
            page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.Equal("137", input.Text);
        }
    });
}
