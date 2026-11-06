using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Editors;
using ZlgEtherCat.UI.Views;
using Xunit;

namespace ZlgEtherCat.Tests;

[CollectionDefinition("Zlg editor runtime", DisableParallelization = true)]
public sealed class ZlgEditorRuntimeCollection;

[Collection("Zlg editor runtime")]
public sealed class GroupedEditorSwitchTests
{
    [StaFact]
    public void EveryActualDropdownPersistsTheSelectedActionAndShowsExactlyItsFields()
    {
        var assembly = typeof(OpenEditorView).Assembly;
        foreach (var (group, operations) in GroupedCases.Expected)
        {
            var plugin = GroupedCases.Plugin(group);
            var serializer = plugin.CreateSerializer();
            var setting = GroupedCases.Setting(group, operations[0]);
            var step = GroupedCases.Step(plugin, setting);
            var editor = (IStepEditorPlugin)Activator.CreateInstance(assembly.GetType($"ZlgEtherCat.UI.Editors.{group}EditorPlugin", true)!)!;
            var view = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(step, null));
            Layout(view);
            var dropdown = view.FindName("OperationEditor") as ComboBox;
            if (operations.Length == 1) Assert.Null(view.FindName("OperationEditor"));
            else Assert.Equal(operations.Length, Assert.IsType<ComboBox>(dropdown).Items.Count);
            foreach (var operation in operations)
            {
                var selected = Enum.Parse(setting.GetType().GetProperty("Operation")!.PropertyType, operation);
                if (dropdown != null) Select(dropdown, selected);
                var model = view.DataContext;
                var modelType = model.GetType();
                modelType.GetMethod("Flush")!.Invoke(model, null);
                var saved = (ZlgGroupedSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
                Assert.Equal(operation, saved.ActiveOperation);
                Assert.False(string.IsNullOrWhiteSpace((string?)modelType.GetProperty("OperationTitle")!.GetValue(model)));
                Assert.False(string.IsNullOrWhiteSpace((string?)modelType.GetProperty("OperationHelp")!.GetValue(model)));
                foreach (var field in setting.GetType().GetProperties().Where(p => p.CanWrite && p.Name != "Operation"))
                {
                    bool active = saved.ActiveFields.Contains(field.Name);
                    Assert.Equal(active, modelType.GetProperty("Is" + field.Name + "Active")!.GetValue(model));
                    var control = Assert.IsAssignableFrom<FrameworkElement>(view.FindName(field.Name + "Editor"));
                    var container = Assert.IsType<StackPanel>(control.Parent);
                    Assert.Equal(active ? Visibility.Visible : Visibility.Collapsed, container.Visibility);
                    if (active) Assert.False(string.IsNullOrWhiteSpace((string?)modelType.GetProperty(field.Name + "Label")!.GetValue(model)));
                }
            }
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    }

    [StaFact]
    public void PdoWriteRetainsMaskedParametersAndSeparateReadPageShowsInputDirection()
    {
        var plugin = new PdoWritePlugin();
        var step = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo });
        var view = (PdoWriteEditorView)new PdoWriteEditorPlugin().CreateEditor(step, null);
        Layout(view);
        var dropdown = (ComboBox)view.FindName("OperationEditor");
        Assert.Contains("输出", view.ViewModel.OffsetLabel);
        Assert.False(view.ViewModel.IsMaskActive);
        view.ViewModel.Data = "new byte[] { 1, 2 }";
        Select(dropdown, PdoWriteOperation.WritePdoMasked);
        Assert.True(view.ViewModel.IsMaskActive);
        view.ViewModel.Mask = "new byte[] { 255, 15 }";
        Select(dropdown, PdoWriteOperation.WritePdo);
        Assert.False(view.ViewModel.IsMaskActive);
        Select(dropdown, PdoWriteOperation.WritePdoMasked);
        view.ViewModel.Flush();
        var saved = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdoMasked, saved.Operation);
        Assert.Equal("new byte[] { 1, 2 }", saved.Data);
        Assert.Equal("new byte[] { 255, 15 }", saved.Mask);
        var read = new PdoReadEditorView();
        read.RefreshFromStep(GroupedCases.Step(new PdoReadPlugin(), new PdoReadSetting()));
        Layout(read);
        Assert.Contains("输入", read.ViewModel.OffsetLabel);
        Assert.Null(read.FindName("DataEditor"));
        Assert.Null(view.FindName("ReturnSampleEditor"));
    }

    [StaFact]
    public void InvalidNumericTextSurvivesSwitchingAndOnlyBlocksItsActiveAction()
    {
        var plugin = new PdoReadPlugin();
        var step = GroupedCases.Step(plugin, new PdoReadSetting { Operation = PdoReadOperation.ReadPdo });
        var view = (PdoReadEditorView)new PdoReadEditorPlugin().CreateEditor(step, null);
        Layout(view);
        var box = (TextBox)view.FindName("LengthEditor");
        box.Text = "not-a-number";
        box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Select((ComboBox)view.FindName("OperationEditor"), PdoReadOperation.ReadPdoBits);
        Assert.Equal("", view.ViewModel.Error);
        Assert.Equal("not-a-number", view.ViewModel.LengthText);
        var inactive = (PdoReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(int.MinValue, inactive.Length);
        Assert.Empty(inactive.Validate());
        Select((ComboBox)view.FindName("OperationEditor"), PdoReadOperation.ReadPdo);
        Assert.Equal("not-a-number", view.ViewModel.LengthText);
        Assert.NotEmpty(view.ViewModel.Error);
        Assert.NotEmpty(((PdoReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).Validate());
    }

    [StaFact]
    public void SynchronousSameStepRefreshPreservesTheSavedFieldsAndNewSelection()
    {
        var plugin = new PdoWritePlugin();
        var step = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo });
        var view = (PdoWriteEditorView)new PdoWriteEditorPlugin().CreateEditor(step, null);
        Layout(view);
        int saves = 0;
        view.ExecuteCommand = (_, save) => { saves++; save(); view.RefreshFromStep(step); };
        view.ViewModel.Offset = 2;
        Select((ComboBox)view.FindName("OperationEditor"), PdoWriteOperation.WritePdoMasked);
        Assert.True(saves >= 2);
        Assert.Equal(PdoWriteOperation.WritePdoMasked, view.ViewModel.Operation);
        Assert.Equal(2, view.ViewModel.Offset);
        var saved = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdoMasked, saved.Operation);
        Assert.Equal(2, saved.Offset);
    }

    [StaFact]
    public void SynchronousDifferentStepRefreshCancelsTheOldStepSelectionChange()
    {
        var plugin = new PdoWritePlugin();
        var first = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo });
        var second = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Offset = 7 });
        var view = (PdoWriteEditorView)new PdoWriteEditorPlugin().CreateEditor(first, null);
        Layout(view);
        view.ExecuteCommand = (_, save) => { save(); view.RefreshFromStep(second); };
        view.ViewModel.Offset = 2;
        Select((ComboBox)view.FindName("OperationEditor"), PdoWriteOperation.WritePdoMasked);
        Assert.Equal(PdoWriteOperation.WritePdo, view.ViewModel.Operation);
        Assert.Equal(7, view.ViewModel.Offset);
        var firstSaved = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        var secondSaved = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdo, firstSaved.Operation);
        Assert.Equal(2, firstSaved.Offset);
        Assert.Equal(PdoWriteOperation.WritePdo, secondSaved.Operation);
        Assert.Equal(7, secondSaved.Offset);
    }

    [StaFact]
    public void FailedOldSaveAfterHostChangesStepKeepsItsErrorAttributedAndCannotChangeTheNewStep()
    {
        var plugin = new PdoWritePlugin();
        var first = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo });
        var second = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Offset = 7 });
        var view = (PdoWriteEditorView)new PdoWriteEditorPlugin().CreateEditor(first, null);
        Layout(view);
        int attempts = 0;
        view.ExecuteCommand = (_, save) =>
        {
            attempts++;
            save();
            if (attempts == 1) view.RefreshFromStep(second);
            if (attempts < 3) throw new IOException("original step host save failed");
        };
        view.ViewModel.Offset = 2;
        Select((ComboBox)view.FindName("OperationEditor"), PdoWriteOperation.WritePdoMasked);
        Assert.Equal(PdoWriteOperation.WritePdo, view.ViewModel.Operation);
        Assert.Equal(7, view.ViewModel.Offset);
        Assert.Contains("原步骤", view.ViewModel.Error);
        Assert.Contains("当前步骤未", view.ViewModel.Error);
        Assert.Throws<IOException>(view.ViewModel.Flush);
        Assert.Equal(2, attempts);
        Assert.Contains("原步骤", view.ViewModel.Error);
        Assert.Contains("当前步骤未", view.ViewModel.Error);
        Assert.Equal(PdoWriteOperation.WritePdo, view.ViewModel.Operation);
        Assert.Equal(7, view.ViewModel.Offset);
        view.ViewModel.Flush();
        Assert.Equal(3, attempts);
        Assert.Equal("", view.ViewModel.Error);
        var saved = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdo, saved.Operation);
        Assert.Equal(2, saved.Offset);
        var untouched = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdo, untouched.Operation);
        Assert.Equal(7, untouched.Offset);
    }

    [StaFact]
    public void FailedOldSaveCannotReplaceANewerQueuedSaveOrClaimItWillRetryTheOldOne()
    {
        var plugin = new PdoWritePlugin();
        var first = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo });
        var second = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Offset = 7 });
        var view = (PdoWriteEditorView)new PdoWriteEditorPlugin().CreateEditor(first, null);
        Layout(view);
        int oldSaves = 0, newSaves = 0;
        view.ExecuteCommand = (_, save) =>
        {
            oldSaves++;
            save();
            view.RefreshFromStep(second);
            view.ExecuteCommand = (_, saveNew) => { newSaves++; saveNew(); };
            view.ViewModel.Offset = 9;
            throw new IOException("old step save failed after new edit");
        };
        view.ViewModel.Offset = 2;
        Select((ComboBox)view.FindName("OperationEditor"), PdoWriteOperation.WritePdoMasked);
        Assert.Equal(PdoWriteOperation.WritePdo, view.ViewModel.Operation);
        Assert.Equal(9, view.ViewModel.Offset);
        Assert.Contains("原步骤", view.ViewModel.Error);
        Assert.DoesNotContain("原待保存内容已保留", view.ViewModel.Error);
        Assert.Contains("替代", view.ViewModel.Error);
        Assert.Equal(1, oldSaves);
        Assert.Equal(0, newSaves);
        Assert.Equal(7, ((PdoWriteSetting)plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, 1)).Offset);
        view.ViewModel.Flush();
        Assert.Equal(1, oldSaves);
        Assert.Equal(1, newSaves);
        Assert.Equal("", view.ViewModel.Error);
        Assert.Equal(2, ((PdoWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1)).Offset);
        var savedNew = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdo, savedNew.Operation);
        Assert.Equal(9, savedNew.Offset);
    }

    [StaFact]
    public void SchedulerTimerEntryPreservesTheOriginalSaveOwnerAcrossFailedExplicitRetry()
    {
        var plugin = new PdoWritePlugin();
        var first = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo });
        var second = GroupedCases.Step(plugin, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Offset = 7 });
        var view = (PdoWriteEditorView)new PdoWriteEditorPlugin().CreateEditor(first, null);
        Layout(view);
        int attempts = 0;
        view.ExecuteCommand = (_, save) =>
        {
            attempts++;
            save();
            if (attempts == 1) view.RefreshFromStep(second);
            if (attempts < 3) throw new IOException("timer started original save failed");
        };
        view.ViewModel.Offset = 2;
        // DispatcherTimer invokes this scheduler entry directly; avoid wall-clock sleeps and unrelated dispatcher work.
        var scheduler = view.ViewModel.GetType().BaseType!.GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view.ViewModel)!;
        var flush = scheduler.GetType().GetMethod("Flush")!;
        var timerFailure = Assert.Throws<TargetInvocationException>(() => flush.Invoke(scheduler, null));
        Assert.IsType<IOException>(timerFailure.InnerException);
        Assert.Equal(1, attempts);
        Assert.Throws<IOException>(view.ViewModel.Flush);
        Assert.Equal(2, attempts);
        Assert.Contains("原步骤", view.ViewModel.Error);
        Assert.Equal(PdoWriteOperation.WritePdo, view.ViewModel.Operation);
        Assert.Equal(7, view.ViewModel.Offset);
        view.ViewModel.Flush();
        Assert.Equal(3, attempts);
        Assert.Equal("", view.ViewModel.Error);
        Assert.Equal(2, ((PdoWriteSetting)plugin.CreateSerializer().Deserialize(first.StepSetting.Setting, 1)).Offset);
        var savedNew = (PdoWriteSetting)plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, 1);
        Assert.Equal(PdoWriteOperation.WritePdo, savedNew.Operation);
        Assert.Equal(7, savedNew.Offset);
    }

    private static void Select(ComboBox dropdown, object operation)
    {
        dropdown.SelectedValue = operation;
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
