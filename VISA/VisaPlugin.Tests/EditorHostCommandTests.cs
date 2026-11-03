using System.Windows;
using VISA.UI.ViewModels;
using VISA.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace VISA.Tests;

public sealed class EditorHostCommandTests
{
    [StaFact]
    public void PendingSaveUsesCapturedHostCommandAndAllowsReentrantRefresh()
    {
        var plugin = new VisaQueryPlugin(); var serializer = plugin.CreateSerializer();
        var step = NewStep(plugin); var vm = new VisaQueryViewModel();
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        int oldCalls = 0, newCalls = 0; string? description = null;
        vm.ExecuteCommand = (_, save) =>
        {
            oldCalls++; save(); description = plugin.GenerateDescription(step.StepSetting.Setting);
            vm.AttachStep(step);
        };
        vm.Command = "\"MEAS:CURR?\"";
        vm.ExecuteCommand = (_, save) => { newCalls++; save(); };
        vm.FlushPendingSave();
        Assert.Equal(1, oldCalls); Assert.Equal(0, newCalls);
        Assert.Contains("MEAS:CURR?", description);
        Assert.Equal(serializer.SettingVersion, step.StepSetting.SettingVersion);
        Assert.Equal("\"MEAS:CURR?\"", vm.Command);
    }

    [StaFact]
    public void FailedHostCommandRemainsRetryableOnItsOriginalOwner()
    {
        var plugin = new VisaQueryPlugin(); var serializer = plugin.CreateSerializer();
        var step = NewStep(plugin); var original = step.StepSetting.Setting.ToArray();
        var vm = new VisaQueryViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        bool fail = true; int oldCalls = 0, replacementCalls = 0;
        vm.ExecuteCommand = (_, save) => { oldCalls++; if (fail) throw new InvalidOperationException("host failure"); save(); };
        vm.Command = "\"MEAS:CURR?\"";
        Assert.Throws<InvalidOperationException>(vm.FlushPendingSave);
        Assert.Equal(original, step.StepSetting.Setting); Assert.NotNull(vm.LastSaveError);
        vm.ExecuteCommand = (_, save) => { replacementCalls++; save(); };
        fail = false; vm.FlushPendingSave();
        Assert.Equal(2, oldCalls); Assert.Equal(0, replacementCalls); Assert.Null(vm.LastSaveError);
        Assert.Contains("MEAS:CURR?", plugin.GenerateDescription(step.StepSetting.Setting));
    }

    [Fact]
    public void EveryActualEditorForwardsInjectedHostCommand() => UiThread.Run(() =>
    {
        string[] names = ["Open", "Close", "Read", "Write", "Query", "BatchWrite", "WaitOpc"];
        foreach (var name in names)
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(VisaQueryPlugin).Assembly.GetType($"VISA.Visa{name}Plugin")!)!;
            var step = NewStep(plugin);
            var view = (FrameworkElement)Activator.CreateInstance(typeof(VisaQueryEditorView).Assembly.GetType($"VISA.UI.Views.Visa{name}EditorView")!)!;
            view.GetType().GetMethod("RefreshFromStep")!.Invoke(view, [step]);
            var vm = view.GetType().GetProperty("ViewModel")!.GetValue(view)!;
            int commands = 0;
            view.GetType().GetProperty("ExecuteCommand")!.SetValue(view, (Action<string, Action>)((_, save) => { commands++; save(); }));
            vm.GetType().GetProperty("ConnectionName")!.SetValue(vm, "\"changed-owner\"");
            vm.GetType().GetMethod("FlushPendingSave")!.Invoke(vm, null);
            Assert.True(commands > 0, name);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    });

    private static Step NewStep(IStepPlugin plugin)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
}
