using System.Collections;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CAN.UI.Views;
using CAN.Models;
using CAN.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class RawCyclicProtectionSelectorTests
{
    private static Step MakeStep()
    {
        var serializer = new CanCyclicSendStartPlugin().CreateSerializer();
        var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanCyclicSendStartSetting
        {
            Messages = [new() { CanId = "\"0x123\"", Protection = new() { RcInitialValue = 3 } },
                new() { CanId = "\"0x123\"", FrameType = CanFrameType.Extended, UseFdFrame = true,
                    Protection = new() { RcInitialValue = 9 } }]
        });
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }

    private static CanCyclicSendStartViewModel Model(Step step)
    {
        var model = new CanCyclicSendStartViewModel();
        model.AttachSerializer(new CanCyclicSendStartPlugin().CreateSerializer()); model.AttachStep(step);
        return model;
    }
    private static PropertyInfo Choices() => Assert.IsAssignableFrom<PropertyInfo>(typeof(CanCyclicSendStartViewModel).GetProperty("ProtectionChoices"));
    private static PropertyInfo Selected() => Assert.IsAssignableFrom<PropertyInfo>(typeof(CanCyclicSendStartViewModel).GetProperty("SelectedProtectionChoice"));

    [WpfFact]
    public void Quick_selection_has_distinguishable_row_identity_and_selects_the_exact_message()
    {
        var model = Model(MakeStep()); var choices = ((IEnumerable)Choices().GetValue(model)!).Cast<object>().ToArray();
        Assert.Equal(2, choices.Length);
        string Label(object value) => (string)value.GetType().GetProperty("Display")!.GetValue(value)!;
        Assert.Contains("1", Label(choices[0])); Assert.Contains("Standard", Label(choices[0])); Assert.Contains("Classic", Label(choices[0]));
        Assert.Contains("2", Label(choices[1])); Assert.Contains("Extended", Label(choices[1])); Assert.Contains("FD", Label(choices[1]));
        Selected().SetValue(model, choices[1]); Assert.Same(model.Messages[1], model.SelectedMessage);
        Assert.Equal(9u, model.SelectedMessage!.Protection.RcInitialValue);
    }

    [WpfFact]
    public void Refreshed_or_removed_choices_cannot_select_a_stale_message()
    {
        var step = MakeStep(); var model = Model(step); var stale = ((IEnumerable)Choices().GetValue(model)!).Cast<object>().Last();
        var oldRow = model.Messages[1]; model.AttachStep(MakeStep());
        var selected = model.SelectedMessage; Selected().SetValue(model, stale);
        Assert.Same(selected, model.SelectedMessage); Assert.DoesNotContain(oldRow, model.Messages);
        model.SelectedMessage = oldRow; Assert.Same(selected, model.SelectedMessage);
    }
    [WpfFact]
    public void Identity_labels_change_without_evaluating_expressions_or_switching_the_selected_message()
    {
        var model = Model(MakeStep()); var selected = model.Messages[1]; model.SelectedMessage = selected;
        var firstChoice = model.ProtectionChoices[0]; int notifications = 0;
        firstChoice.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CyclicProtectionChoice.Display)) notifications++; };
        model.Messages[0].CanId = "FileGlobals.DynamicCanId";
        model.Messages[0].FrameType = CanFrameType.Extended; model.Messages[0].UseFdFrame = true;
        Assert.Contains("FileGlobals.DynamicCanId", firstChoice.Display); Assert.Contains("Extended", firstChoice.Display);
        Assert.Contains("CAN FD", firstChoice.Display); Assert.Equal(3, notifications);
        Assert.Same(selected, model.SelectedMessage); model.Flush();
    }

    [WpfFact]
    public void Removing_rows_reindexes_choices_and_empty_state_drops_old_protection()
    {
        var step = MakeStep(); var model = Model(step); model.SelectedProtectionChoice = model.ProtectionChoices[1];
        var stale = model.ProtectionChoices[0]; var remaining = model.Messages[1];
        model.RemoveMessage(model.Messages[0]);
        Assert.Single(model.ProtectionChoices); Assert.StartsWith("第 1 行", model.ProtectionChoices[0].Display);
        Assert.Same(remaining, model.SelectedMessage); model.SelectedProtectionChoice = stale;
        Assert.Same(remaining, model.SelectedMessage);
        model.RemoveMessage(remaining); Assert.Empty(model.ProtectionChoices); Assert.Null(model.SelectedMessage);
        Assert.Null(model.SelectedProtectionChoice); Assert.False(model.CanSelectProtection);
        model.AddMessage(); Assert.Same(Assert.Single(model.Messages), model.SelectedMessage); Assert.True(model.CanSelectProtection);
        model.Flush();
    }

    [WpfFact]
    public void Choice_edits_flush_to_owning_step_and_unloaded_callbacks_cannot_save()
    {
        var first = MakeStep(); var next = MakeStep(); var model = Model(first);
        model.SelectedProtectionChoice = model.ProtectionChoices[1]; var oldRow = model.SelectedMessage!;
        oldRow.Protection.RcInitialValue = 12; model.AttachStep(next);
        var serializer = new CanCyclicSendStartPlugin().CreateSerializer();
        var saved = (CanCyclicSendStartSetting)serializer.Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal(3u, saved.Messages[0].Protection.RcInitialValue); Assert.Equal(12u, saved.Messages[1].Protection.RcInitialValue);
        var unchanged = next.StepSetting.Setting.ToArray(); oldRow.Protection.RcInitialValue = 77; model.Flush();
        Assert.Equal(unchanged, next.StepSetting.Setting);
        var staleChoice = model.ProtectionChoices[1]; model.Deactivate();
        staleChoice.Message.Protection.RcInitialValue = 99; model.SelectedProtectionChoice = staleChoice; model.Flush();
        Assert.Equal(unchanged, next.StepSetting.Setting); Assert.False(model.CanSelectProtection);
        model.Activate(); Assert.Equal(9u, model.Messages[1].Protection.RcInitialValue);
        model.SelectedProtectionChoice = staleChoice; Assert.Same(model.Messages[0], model.SelectedMessage);
    }

    internal static void VerifyActualPage()
    {
        var step = MakeStep(); var view = new CanCyclicSendStartEditorView(); view.RefreshFromStep(step); Layout(view);
        var selector = Assert.IsType<ComboBox>(view.FindName("ProtectionMessageSelector"));
        var grid = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        var editor = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        Assert.Equal(2, selector.Items.Count); selector.SelectedIndex = 1; Layout(view);
        Assert.Same(view.ViewModel.Messages[1], grid.SelectedItem); Assert.Same(view.ViewModel.Messages[1].Protection, editor.DataContext);
        Assert.Contains("Extended", ((CyclicProtectionChoice)selector.SelectedItem).Display);
        Assert.IsType<CheckBox>(editor.FindName("RcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox")).Text = "12";
        grid.SelectedItem = view.ViewModel.Messages[0]; Layout(view); Assert.Equal(0, selector.SelectedIndex);
        Assert.Equal("3", Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox")).Text);
        selector.SelectedIndex = 1; Layout(view); Assert.Equal("12", Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox")).Text);
        var scroll = VisualParents(selector).OfType<ScrollViewer>().First();
        scroll.ScrollToVerticalOffset(selector.TranslatePoint(new Point(), scroll).Y - 30); Layout(view); Capture(view);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(3u, saved.Messages[0].Protection.RcInitialValue); Assert.False(saved.Messages[0].Protection.RcEnabled);
        Assert.Equal(12u, saved.Messages[1].Protection.RcInitialValue); Assert.True(saved.Messages[1].Protection.RcEnabled);
        var previous = view.ViewModel.ProtectionChoices[1]; view.RefreshFromStep(MakeStep()); selector.SelectedItem = previous;
        Assert.DoesNotContain(previous.Message, view.ViewModel.Messages); Assert.Same(view.ViewModel.Messages[0].Protection, editor.DataContext);
        view.ViewModel.Messages.Clear(); Layout(view); Assert.Empty(selector.Items); Assert.False(selector.IsEnabled); Assert.Null(editor.DataContext);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }
    private static IEnumerable<DependencyObject> VisualParents(DependencyObject item)
    { while (VisualTreeHelper.GetParent(item) is { } parent) { yield return parent; item = parent; } }
    private static void Layout(FrameworkElement view)
    { view.Measure(new Size(1250, 1000)); view.Arrange(new Rect(0, 0, 1250, 1000)); view.UpdateLayout(); }
    private static void Capture(FrameworkElement view)
    {
        string? folder = Environment.GetEnvironmentVariable("CAN_CYCLIC_SELECTOR_SCREENSHOTS"); if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder); var bitmap = new RenderTargetBitmap(1250, 1000, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(folder, "can-cyclic-rc-crc-selector.png")); encoder.Save(file);
    }
}
