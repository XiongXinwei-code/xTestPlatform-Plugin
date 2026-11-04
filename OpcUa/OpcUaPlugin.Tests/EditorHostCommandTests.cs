using System.Windows;
using OpcUa.Models;
using OpcUa.UI.ViewModels;
using OpcUa.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace OpcUa.Tests;

public sealed class EditorHostCommandTests
{
    [StaFact]
    public void PendingSaveUsesCapturedHostCommandAndAllowsReentrantRefresh()
    {
        var plugin = new OpcUaConnectPlugin();
        var serializer = plugin.CreateSerializer();
        var step = NewStep(plugin);
        var vm = new OpcUaConnectViewModel();
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        int oldCalls = 0, newCalls = 0;
        string? description = null;
        vm.ExecuteCommand = (_, save) =>
        {
            oldCalls++; save();
            description = plugin.GenerateDescription(step.StepSetting.Setting);
            vm.AttachStep(step);
        };
        vm.EndpointUrl = "\"opc.tcp://updated:4840\"";
        vm.ExecuteCommand = (_, save) => { newCalls++; save(); };
        vm.FlushPendingSave();
        Assert.Equal(1, oldCalls); Assert.Equal(0, newCalls);
        Assert.Contains("updated:4840", description);
        Assert.Equal(serializer.SettingVersion, step.StepSetting.SettingVersion);
        Assert.Equal("\"opc.tcp://updated:4840\"", vm.EndpointUrl);
    }

    [StaFact]
    public void FailedHostCommandRemainsRetryableOnItsOriginalOwner()
    {
        var plugin = new OpcUaConnectPlugin(); var serializer = plugin.CreateSerializer();
        var step = NewStep(plugin); var original = step.StepSetting.Setting.ToArray();
        var vm = new OpcUaConnectViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        bool fail = true; int oldCalls = 0, replacementCalls = 0;
        vm.ExecuteCommand = (_, save) => { oldCalls++; if (fail) throw new InvalidOperationException("host failure"); save(); };
        vm.EndpointUrl = "\"opc.tcp://retry:4840\"";
        Assert.Throws<InvalidOperationException>(vm.FlushPendingSave);
        Assert.Equal(original, step.StepSetting.Setting); Assert.NotNull(vm.LastSaveError);
        vm.ExecuteCommand = (_, save) => { replacementCalls++; save(); };
        fail = false; vm.FlushPendingSave();
        Assert.Equal(2, oldCalls); Assert.Equal(0, replacementCalls); Assert.Null(vm.LastSaveError);
        Assert.Contains("retry:4840", plugin.GenerateDescription(step.StepSetting.Setting));
    }

    [Fact]
    public void EveryActualEditorForwardsInjectedHostCommand() => UiThread.Run(() =>
    {
        string[] names = ["Connect", "Disconnect", "Read", "Write", "BatchRead", "BatchWrite", "Subscribe",
            "DataAcqStart", "DataAcqRead", "DataAcqStop"];
        foreach (var name in names)
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(OpcUaConnectPlugin).Assembly.GetType($"OpcUa.OpcUa{name}Plugin")!)!;
            var step = NewStep(plugin);
            var view = (FrameworkElement)Activator.CreateInstance(typeof(OpcUaConnectEditorView).Assembly.GetType($"OpcUa.UI.Views.OpcUa{name}EditorView")!)!;
            view.GetType().GetMethod("RefreshFromStep")!.Invoke(view, [step]);
            var vm = view.GetType().GetProperty("ViewModel")!.GetValue(view)!;
            int commands = 0;
            view.GetType().GetProperty("ExecuteCommand")!.SetValue(view, (Action<string, Action>)((_, save) => { commands++; save(); }));
            var field = vm.GetType().GetProperty("ConnectionName") ?? vm.GetType().GetProperty("TaskName");
            Assert.NotNull(field); field.SetValue(vm, "\"changed-owner\"");
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
