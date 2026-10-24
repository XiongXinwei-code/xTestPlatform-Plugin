using VISA.Models;
using VISA.UI.ViewModels;
using VISA.UI.Views;
using System.Windows;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace VISA.Tests;

public sealed class EditorTests
{
    [StaFact]
    public void RefreshBeforeDebounceDoesNotDiscardCommand()
    {
        var serializer = new VisaQueryPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new VisaQuerySetting());
        var vm = new VisaQueryViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.Command = "\"MEAS:VOLT?\"";
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        Assert.Equal("\"MEAS:VOLT?\"", vm.Command);
    }

    [Fact]
    public void ActualEditorFlushesOnUnload() => UiThread.Run(() =>
    {
        var serializer = new VisaQueryPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new VisaQuerySetting());
        var view = new VisaQueryEditorView(); view.RefreshFromStep(step);
        view.ViewModel.Command = "\"MEAS:CURR?\"";
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal("\"MEAS:CURR?\"", ((VisaQuerySetting)serializer.Deserialize(step.StepSetting.Setting, 1)).Command);
    });

    [StaFact]
    public void BatchRowEditsPersistAndOldRowsCannotModifyNewBinding()
    {
        var serializer = new VisaBatchWritePlugin().CreateSerializer();
        Step MakeStep()
        {
            var step = new Step(); step.StepSetting.SettingVersion = 1;
            step.StepSetting.Setting = serializer.Serialize(new VisaBatchWriteSetting()); return step;
        }
        var first = MakeStep(); var second = MakeStep();
        var vm = new VisaBatchWriteViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(first);
        vm.AddItem(); var old = vm.Items[0]; old.Command = "\"OUTP OFF\""; old.DelayMs = 23;
        vm.AttachStep(second);
        var saved = (VisaBatchWriteSetting)serializer.Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal("\"OUTP OFF\"", Assert.Single(saved.Items).Command);
        Assert.Equal(23, saved.Items[0].DelayMs);
        var before = second.StepSetting.Setting.ToArray(); old.Command = "\"MUST NOT SAVE\""; vm.FlushPendingSave();
        Assert.Equal(before, second.StepSetting.Setting);
    }
}
