using CAN.Models;
using CAN.UI.ViewModels;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class RawProtectionRefreshTests
{
    [StaFact]
    public void MultiWritePreservesProtectionSelectionAcrossHostRefresh()
    {
        var plugin = new CanWritePlugin(); var serializer = plugin.CreateSerializer();
        var setting = new CanWriteSetting { UseMessageList = true, Messages =
            [new() { CanId = "\"0x101\"", Protection = new() { RcInitialValue = 1 } },
             new() { CanId = "\"0x202\"", Protection = new() { RcInitialValue = 2 } }] };
        var first = NewStep(serializer, setting); var second = NewStep(serializer, setting);
        var vm = new CanWriteViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(first);
        vm.SelectedMessage = vm.Messages[1];
        int commands = 0;
        vm.ExecuteCommand = (_, save) =>
        {
            commands++; save();
            vm.AttachSerializer(serializer); vm.AttachStep(first);
        };
        vm.PropertyChanged += (_, e) =>
        {
            // ItemsSource replacement may temporarily clear a two-way selector.
            if (string.IsNullOrEmpty(e.PropertyName)) vm.SelectedMessage = null;
        };
        vm.SelectedMessage!.Protection.RcInitialValue = 11;
        vm.Flush();
        Assert.Same(vm.Messages[1], vm.SelectedMessage);
        Assert.Contains("0x202", vm.SelectedProtectionTitle);
        vm.SelectedMessage!.Protection.RcStep = 3;
        vm.Flush();
        Assert.Equal(2, commands);
        var saved = (CanWriteSetting)serializer.Deserialize(first.StepSetting.Setting, serializer.SettingVersion);
        Assert.Equal(1u, saved.Messages[0].Protection.RcInitialValue);
        Assert.Equal(1L, saved.Messages[0].Protection.RcStep);
        Assert.Equal(11u, saved.Messages[1].Protection.RcInitialValue);
        Assert.Equal(3L, saved.Messages[1].Protection.RcStep);
        vm.AttachStep(second);
        Assert.Same(vm.Messages[0], vm.SelectedMessage);
    }

    [StaFact]
    public void CyclicWritePreservesProtectionSelectionAcrossHostRefresh()
    {
        var plugin = new CanCyclicSendStartPlugin(); var serializer = plugin.CreateSerializer();
        var setting = new CanCyclicSendStartSetting { Messages =
            [new() { CanId = "\"0x101\"", Protection = new() { RcInitialValue = 1 } },
             new() { CanId = "\"0x202\"", Protection = new() { RcInitialValue = 2 } }] };
        var first = NewStep(serializer, setting); var second = NewStep(serializer, setting);
        var vm = new CanCyclicSendStartViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(first);
        vm.SelectedProtectionChoice = vm.ProtectionChoices[1];
        int commands = 0;
        vm.ExecuteCommand = (_, save) =>
        {
            commands++; save();
            vm.AttachSerializer(serializer); vm.AttachStep(first);
        };
        vm.SelectedMessage!.Protection.RcInitialValue = 11;
        vm.Flush();
        Assert.Same(vm.Messages[1], vm.SelectedMessage);
        Assert.Same(vm.ProtectionChoices[1], vm.SelectedProtectionChoice);
        Assert.Contains("0x202", vm.SelectedProtectionTitle);
        vm.SelectedMessage!.Protection.RcStep = 3;
        vm.Flush();
        Assert.Equal(2, commands);
        var saved = (CanCyclicSendStartSetting)serializer.Deserialize(first.StepSetting.Setting, serializer.SettingVersion);
        Assert.Equal(1u, saved.Messages[0].Protection.RcInitialValue);
        Assert.Equal(1L, saved.Messages[0].Protection.RcStep);
        Assert.Equal(11u, saved.Messages[1].Protection.RcInitialValue);
        Assert.Equal(3L, saved.Messages[1].Protection.RcStep);
        vm.AttachStep(second);
        Assert.Same(vm.Messages[0], vm.SelectedMessage);
        Assert.Same(vm.ProtectionChoices[0], vm.SelectedProtectionChoice);
    }

    private static Step NewStep(IStepSettingSerializer serializer, object setting)
    {
        var step = new Step(); step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        return step;
    }
}
