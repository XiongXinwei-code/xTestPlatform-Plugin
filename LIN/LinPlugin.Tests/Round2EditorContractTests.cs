using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using LIN.Models;
using LIN.UI.Editors;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace LIN.Tests;

public sealed class Round2EditorContractTests
{
    [Theory]
    [InlineData("Read", "ConnectionName")]
    [InlineData("Read", "FilterFrameId")]
    [InlineData("Write", "ConnectionName")]
    [InlineData("Write", "FrameId")]
    [InlineData("Write", "Data")]
    [InlineData("WriteRead", "ConnectionName")]
    [InlineData("WriteRead", "FrameId")]
    [InlineData("WriteRead", "Data")]
    [InlineData("CyclicSendStop", "TaskName")]
    public async Task RemainingExpressionFieldsAreValidated(string name, string field)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(LinOpenPlugin).Assembly.GetType($"LIN.Lin{name}Plugin")!)!;
        var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(LinOpenEditorPlugin).Assembly.GetType($"LIN.UI.Editors.Lin{name}EditorPlugin")!)!;
        var setting = plugin.CreateSerializer().CreateDefault(); setting.GetType().GetProperty(field)!.SetValue(setting, ")");
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(plugin, setting)), e => e.Message.Contains(field, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Channel")]
    [InlineData("ConnectionName")]
    [InlineData("TaskName")]
    [InlineData("FrameId")]
    [InlineData("Data")]
    public async Task EveryExpressionIsValidated(string field)
    {
        IStepPlugin plugin = field == "Channel" ? new LinOpenPlugin() : new LinCyclicSendStartPlugin();
        IStepEditorPlugin editor = field == "Channel" ? new LinOpenEditorPlugin() : new LinCyclicSendStartEditorPlugin();
        object setting = plugin.CreateSerializer().CreateDefault();
        object target = setting;
        if (field is "FrameId" or "Data") { var s = (LinCyclicSendStartSetting)setting; s.Frames = [new() { Enabled = true }]; target = s.Frames[0]; }
        target.GetType().GetProperty(field)!.SetValue(target, ")");
        var messages = await editor.ValidateWithContextAsync(Validation(plugin, setting));
        Assert.Contains(messages, e => e.Message.Contains(field, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownWakeSleepEnumIsRejectedByEditorAndRuntime(bool wake)
    {
        IStepPlugin plugin = wake ? new LinWakeupPlugin() : new LinSleepPlugin();
        IStepEditorPlugin editor = wake ? new LinWakeupEditorPlugin() : new LinSleepEditorPlugin();
        object setting = wake ? new LinWakeupSetting { WakeupMode = (LinWakeupMode)77, PostWakeupDelayMs = 0 } : new LinSleepSetting { SleepMode = (LinSleepMode)77, PostSleepDelayMs = 0 };
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(plugin, setting)), e => e.Code == "LIN_MODE");
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = plugin; state.Setting = setting;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("模式", result.StepResult.Error!.Message);
    }

    [Fact]
    public async Task NullExpressionIsNotUsedAsLiteralHardwareChannel()
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = new LinOpenPlugin(); state.Setting = new LinOpenSetting { Channel = "null" };
        int factories = 0;
        var result = await new LIN.Executors.LinOpenExecutor(_ => { factories++; throw new Exception("unexpected factory"); }).ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, factories);
        Assert.Contains("null", result.StepResult.Error!.Message);
    }

    internal static void ActualDeleteRemovesSelectedRow()
    {
        var plugin = new LinCyclicSendStartPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new LinCyclicSendStartSetting { Frames = [new() { FrameId = "\"0x11\"" }, new() { FrameId = "\"0x22\"" }] });
        step.StepSetting.SettingVersion = 1;
        var view = new LinCyclicSendStartEditorView(); view.RefreshFromStep(step);
        view.Measure(new Size(1000, 700)); view.Arrange(new Rect(0, 0, 1000, 700)); view.UpdateLayout();
        var grid = Tree(view).OfType<DataGrid>().Single(); grid.SelectedItem = view.ViewModel.Frames[0];
        var remove = Tree(view).OfType<Button>().Single(b => Equals(b.Content, "Remove"));
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (LinCyclicSendStartSetting)serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.Single(saved.Frames); Assert.Equal("\"0x22\"", saved.Frames[0].FrameId);
    }

    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var nested in Tree(child)) yield return nested;
    }
    static StepEditorValidationContext Validation(IStepPlugin plugin, object setting)
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; var step = context.CurrentStep!.Step;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(), ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default };
    }
}
