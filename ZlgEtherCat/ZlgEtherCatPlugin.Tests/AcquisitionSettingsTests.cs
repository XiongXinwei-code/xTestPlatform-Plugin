using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using ZlgEtherCat.Models;
using Operations = ZlgEtherCat.Models.Operations;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Editors;
using ZlgEtherCat.UI.Views;
using Xunit;

namespace ZlgEtherCat.Tests;

[Collection("Zlg editor runtime")]
public sealed class AcquisitionSettingsTests
{
    private static readonly string[] AcquisitionFields =
    [
        nameof(StartAcquisitionSetting.AcquisitionThreadPriority),
        nameof(StartAcquisitionSetting.RxHighWatermarkPercent),
        nameof(StartAcquisitionSetting.RxBacklogWarningMs),
        nameof(StartAcquisitionSetting.RxAlarmRepeatMs)
    ];

    [Fact]
    public void NewAcquisitionDefaultsRemainExplicitAndValid()
    {
        var setting = Assert.IsType<StartAcquisitionSetting>(new StartAcquisitionPlugin().CreateSerializer().CreateDefault());
        Assert.Equal(StartAcquisitionOperation.StartAcquisition, setting.Operation);
        Assert.Equal(AcquisitionPriority.Normal, setting.AcquisitionThreadPriority);
        Assert.Equal(80, setting.RxHighWatermarkPercent);
        Assert.Equal(10, setting.RxBacklogWarningMs);
        Assert.Equal(1000, setting.RxAlarmRepeatMs);
        Assert.Empty(setting.Validate());
    }

    [Theory]
    [InlineData(AcquisitionPriority.Normal)]
    [InlineData(AcquisitionPriority.AboveNormal)]
    [InlineData(AcquisitionPriority.Highest)]
    public async Task EveryPriorityAndThresholdSurvivesStorageAndProjectsIntoTheStartRequest(AcquisitionPriority priority)
    {
        var plugin = new StartAcquisitionPlugin();
        var serializer = plugin.CreateSerializer();
        var setting = new StartAcquisitionSetting
        {
            Operation = StartAcquisitionOperation.StartAcquisition,
            AcquisitionThreadPriority = priority,
            RxHighWatermarkPercent = 73,
            RxBacklogWarningMs = 25,
            RxAlarmRepeatMs = 1500
        };
        var restored = Assert.IsType<StartAcquisitionSetting>(serializer.Deserialize(serializer.Serialize(setting), serializer.SettingVersion));
        var action = Assert.IsType<Operations.StartAcquisitionSetting>(restored.CreateOperationSetting());
        Assert.Equal(priority, restored.AcquisitionThreadPriority);
        Assert.Equal(priority, action.AcquisitionThreadPriority);
        Assert.Equal(73, action.RxHighWatermarkPercent);
        Assert.Equal(25, action.RxBacklogWarningMs);
        Assert.Equal(1500, action.RxAlarmRepeatMs);
        Assert.Empty(await ValidateWithContext(restored));

        var request = await restored.ToRequestAsync(ExecutionContext(), default);
        Assert.Equal("ECAT1", request.ConnectionName);
        Assert.Equal(priority, request.Get<AcquisitionPriority>(nameof(StartAcquisitionSetting.AcquisitionThreadPriority)));
        Assert.Equal(73, request.Get<int>(nameof(StartAcquisitionSetting.RxHighWatermarkPercent)));
        Assert.Equal(25, request.Get<int>(nameof(StartAcquisitionSetting.RxBacklogWarningMs)));
        Assert.Equal(1500, request.Get<int>(nameof(StartAcquisitionSetting.RxAlarmRepeatMs)));
        Assert.All(AcquisitionFields, field => Assert.Contains(field, restored.ActiveFields));
    }

    [Theory]
    [InlineData(nameof(StartAcquisitionSetting.RxHighWatermarkPercent), 1, true)]
    [InlineData(nameof(StartAcquisitionSetting.RxHighWatermarkPercent), 100, true)]
    [InlineData(nameof(StartAcquisitionSetting.RxHighWatermarkPercent), 0, false)]
    [InlineData(nameof(StartAcquisitionSetting.RxHighWatermarkPercent), 101, false)]
    [InlineData(nameof(StartAcquisitionSetting.RxBacklogWarningMs), 1, true)]
    [InlineData(nameof(StartAcquisitionSetting.RxBacklogWarningMs), 60000, true)]
    [InlineData(nameof(StartAcquisitionSetting.RxBacklogWarningMs), 0, false)]
    [InlineData(nameof(StartAcquisitionSetting.RxBacklogWarningMs), 60001, false)]
    [InlineData(nameof(StartAcquisitionSetting.RxAlarmRepeatMs), 100, true)]
    [InlineData(nameof(StartAcquisitionSetting.RxAlarmRepeatMs), 3600000, true)]
    [InlineData(nameof(StartAcquisitionSetting.RxAlarmRepeatMs), 99, false)]
    [InlineData(nameof(StartAcquisitionSetting.RxAlarmRepeatMs), 3600001, false)]
    public async Task ActiveThresholdBoundariesAreEnforcedByTheActualEditor(string field, int value, bool valid)
    {
        var setting = new StartAcquisitionSetting();
        switch (field)
        {
            case nameof(StartAcquisitionSetting.RxHighWatermarkPercent): setting.RxHighWatermarkPercent = value; break;
            case nameof(StartAcquisitionSetting.RxBacklogWarningMs): setting.RxBacklogWarningMs = value; break;
            case nameof(StartAcquisitionSetting.RxAlarmRepeatMs): setting.RxAlarmRepeatMs = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }

        var errors = await ValidateWithContext(setting);
        if (valid)
        {
            Assert.Empty(errors);
            var request = await setting.ToRequestAsync(ExecutionContext(), default);
            Assert.Equal(value, request.Get<int>(field));
        }
        else
        {
            Assert.Contains(errors, error => error.Code == "ZEC_001");
            await Assert.ThrowsAsync<ArgumentException>(() => setting.ToRequestAsync(ExecutionContext(), default));
        }
    }

    [Theory]
    [InlineData("StopAcquisition")]
    [InlineData("ReadAcquisitionStatus")]
    [InlineData("ReadHistory")]
    public async Task OtherAcquisitionStepsNeitherStoreNorProjectStartOnlyValues(string operation)
    {
        var setting = GroupedCases.Setting(operation, operation);
        Assert.Empty(setting.Validate());
        var request = await setting.ToRequestAsync(ExecutionContext(), default);
        foreach (string field in AcquisitionFields)
        {
            Assert.Null(setting.GetType().GetProperty(field));
            Assert.DoesNotContain(field, setting.ActiveFields);
            Assert.False(request.Arguments.ContainsKey(field));
        }
    }

    [Fact]
    public void UndefinedPriorityCannotBeStoredOrValidatedAsAStartConfiguration()
    {
        var setting = new StartAcquisitionSetting { AcquisitionThreadPriority = (AcquisitionPriority)99 };
        Assert.Contains(setting.Validate(), error => error.Contains("优先级", StringComparison.Ordinal));
        Assert.ThrowsAny<Exception>(() => new StartAcquisitionPlugin().CreateSerializer().Serialize(setting));
    }

    [StaFact]
    public void IndependentStartAndStopPagesFlushOnlyTheirOwnStepAndPreserveAllStartControls()
    {
        var plugin = new StartAcquisitionPlugin();
        var step = GroupedCases.Step(plugin, new StartAcquisitionSetting());
        var view = Assert.IsType<StartAcquisitionEditorView>(new StartAcquisitionEditorPlugin().CreateEditor(step, null));
        Layout(view);
        Assert.Null(view.FindName("OperationEditor"));
        var stopPlugin = new StopAcquisitionPlugin();
        var stopStep = GroupedCases.Step(stopPlugin, new StopAcquisitionSetting());
        var stopView = Assert.IsType<StopAcquisitionEditorView>(new StopAcquisitionEditorPlugin().CreateEditor(stopStep, null));
        Layout(stopView);
        Assert.Null(stopView.FindName("OperationEditor"));
        foreach (string field in AcquisitionFields) Assert.Null(stopView.FindName(field + "Editor"));
        byte[] originalStop = (byte[])stopStep.StepSetting.Setting.Clone();
        var priority = Assert.IsType<ComboBox>(view.FindName("AcquisitionThreadPriorityEditor"));
        var choices = priority.Items.Cast<KeyValuePair<AcquisitionPriority, string>>().ToArray();
        Assert.Equal(new[] { AcquisitionPriority.Normal, AcquisitionPriority.AboveNormal, AcquisitionPriority.Highest }, choices.Select(choice => choice.Key));
        Assert.Contains("普通", choices[0].Value);
        Assert.Contains("高于普通", choices[1].Value);
        Assert.Contains("最高", choices[2].Value);

        foreach (var selected in Enum.GetValues<AcquisitionPriority>())
        {
            Select(priority, selected);
            SetText(view, "RxHighWatermarkPercentEditor", "75");
            SetText(view, "RxBacklogWarningMsEditor", "40");
            SetText(view, "RxAlarmRepeatMsEditor", "2000");
            view.ViewModel.Flush();
            Assert.Equal(originalStop, stopStep.StepSetting.Setting);
            AssertSaved(step, plugin, StartAcquisitionOperation.StartAcquisition, selected);
            Assert.Equal(selected, priority.SelectedValue);
            Assert.Equal("75", view.ViewModel.RxHighWatermarkPercentText);
            Assert.Equal("40", view.ViewModel.RxBacklogWarningMsText);
            Assert.Equal("2000", view.ViewModel.RxAlarmRepeatMsText);
            foreach (string field in AcquisitionFields)
            {
                var control = Assert.IsAssignableFrom<FrameworkElement>(view.FindName(field + "Editor"));
                Assert.Equal(Visibility.Visible, Assert.IsType<StackPanel>(control.Parent).Visibility);
            }
        }
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        AssertSaved(step, plugin, StartAcquisitionOperation.StartAcquisition, AcquisitionPriority.Highest);
        byte[] originalStart = (byte[])step.StepSetting.Setting.Clone();
        SetText(stopView, "TimeoutMsEditor", "4321");
        stopView.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(4321, ((StopAcquisitionSetting)stopPlugin.CreateSerializer().Deserialize(stopStep.StepSetting.Setting, 1)).TimeoutMs);
        Assert.Equal(originalStart, step.StepSetting.Setting);
    }

    private static IExecutionContext ExecutionContext() => DispatchProxy.Create<IExecutionContext, EditorContextProxy>();

    private static Task<IReadOnlyList<StepSettingError>> ValidateWithContext(StartAcquisitionSetting setting)
    {
        var current = GroupedCases.Step(new StartAcquisitionPlugin(), setting);
        var predecessor = GroupedCases.Step(new EnablePiPlugin(), new EnablePiSetting());
        return new StartAcquisitionEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = current.StepSetting.Setting,
            CurrentStep = current,
            Block = [predecessor, current],
            SequenceFile = new SequenceFile(),
            ExecutionContext = ExecutionContext(),
            Evaluator = ExpressionEvaluatorFactory.Default
        });
    }

    private static void AssertSaved(Step step, StartAcquisitionPlugin plugin, StartAcquisitionOperation operation, AcquisitionPriority priority)
    {
        var saved = Assert.IsType<StartAcquisitionSetting>(plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion));
        Assert.Equal(operation, saved.Operation);
        Assert.Equal(priority, saved.AcquisitionThreadPriority);
        Assert.Equal(75, saved.RxHighWatermarkPercent);
        Assert.Equal(40, saved.RxBacklogWarningMs);
        Assert.Equal(2000, saved.RxAlarmRepeatMs);
        Assert.Empty(saved.Validate());
    }

    private static void SetText(UserControl view, string name, string text)
    {
        var control = Assert.IsType<TextBox>(view.FindName(name));
        control.Text = text;
        control.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
    }

    private static void Select(ComboBox dropdown, object value)
    {
        dropdown.SelectedValue = value;
        dropdown.GetBindingExpression(ComboBox.SelectedValueProperty)!.UpdateSource();
        dropdown.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(900, 700));
        view.Arrange(new Rect(0, 0, 900, 700));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }
}
