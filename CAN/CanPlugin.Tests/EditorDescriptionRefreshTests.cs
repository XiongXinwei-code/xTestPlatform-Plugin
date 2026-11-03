using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using CAN.Models;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using Xunit;
using xTestPlatform.Core.SequenceModels;

namespace CAN.Tests;

public sealed class EditorDescriptionRefreshTests
{
    private static Step MakeStep()
    {
        var plugin = new CanOpenPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
        step.PropertiesSetting.General.StepDescription = plugin.GenerateDescription(step.StepSetting.Setting);
        return step;
    }
    private static CanOpenViewModel Attach(Step step)
    {
        var model = new CanOpenViewModel(); model.AttachSerializer(new CanOpenPlugin().CreateSerializer()); model.AttachStep(step); return model;
    }
    // The installed host owns General and calls GenerateDescription after the
    // supplied action. This fixture models that public contract, not host source.
    private static Action<string, Action> Host(Step step, Action? after = null) => (_, change) =>
    {
        change();
        step.PropertiesSetting.General.StepDescription = new CanOpenPlugin().GenerateDescription(step.StepSetting.Setting);
        after?.Invoke();
    };

    [WpfFact]
    public async Task Debounced_edits_commit_through_host_and_refresh_description()
    {
        var step = MakeStep(); var model = Attach(step); int commits = 0;
        model.ExecuteCommand = Host(step, () => commits++);
        model.AdapterType = (int)CanAdapterType.ZLG;
        model.BaudRate = 250000;
        Assert.Equal(0, commits);
        for (int i = 0; i < 40 && commits == 0; i++) await Task.Delay(25);
        Assert.Equal(1, commits);
        Assert.Contains("ZLG", step.PropertiesSetting.General.StepDescription);
        Assert.Contains("250000", step.PropertiesSetting.General.StepDescription);
    }

    [WpfFact]
    public void Pending_edit_keeps_original_step_and_host_when_editor_is_rebound()
    {
        var first = MakeStep(); var second = MakeStep(); var model = Attach(first); int a = 0, b = 0;
        model.ExecuteCommand = Host(first, () => a++);
        model.AdapterType = (int)CanAdapterType.ZLG;
        model.ExecuteCommand = Host(second, () => b++);
        model.AttachStep(second); model.FlushPendingSave();
        Assert.Equal(1, a); Assert.Equal(0, b);
        Assert.Contains("ZLG", first.PropertiesSetting.General.StepDescription);
        Assert.Contains("NI", second.PropertiesSetting.General.StepDescription);
    }

    [WpfFact]
    public void Switching_steps_survives_synchronous_refresh_of_the_previous_step()
    {
        var first = MakeStep(); var second = MakeStep(); var model = Attach(first);
        var serializer = new CanOpenPlugin().CreateSerializer();
        var secondSetting = (CanOpenSetting)serializer.CreateDefault(); secondSetting.ConnectionName = "\"SECOND\"";
        second.StepSetting.Setting = serializer.Serialize(secondSetting);
        model.ExecuteCommand = Host(first, () => model.AttachStep(first));
        model.BaudRate = 250000;
        model.AttachStep(second);
        Assert.Equal("\"SECOND\"", model.ConnectionName);
        model.ExecuteCommand = Host(second);
        model.BaudRate = 125000; model.FlushPendingSave();
        Assert.Equal(250000, ((CanOpenSetting)serializer.Deserialize(first.StepSetting.Setting, 1)).BaudRate);
        Assert.Equal(125000, ((CanOpenSetting)serializer.Deserialize(second.StepSetting.Setting, 1)).BaudRate);
    }

    [WpfFact]
    public void Host_refresh_during_commit_does_not_reenter_old_save()
    {
        var step = MakeStep(); var model = Attach(step); int commits = 0;
        model.ExecuteCommand = Host(step, () => { commits++; model.AttachStep(step); });
        model.AdapterType = (int)CanAdapterType.ZLG; model.FlushPendingSave();
        Assert.Equal(1, commits); Assert.Equal((int)CanAdapterType.ZLG, model.AdapterType);
        Assert.Contains("ZLG", step.PropertiesSetting.General.StepDescription);
    }

    [WpfFact]
    public void Failed_host_command_keeps_pending_edit_and_original_command_for_retry()
    {
        var step = MakeStep(); var model = Attach(step); bool fail = true; int wrong = 0;
        model.ExecuteCommand = (name, save) => { if (fail) throw new InvalidOperationException("host rejected"); Host(step)(name, save); };
        model.AdapterType = (int)CanAdapterType.ZLG;
        Assert.Throws<InvalidOperationException>(model.FlushPendingSave);
        model.ExecuteCommand = (_, _) => wrong++;
        fail = false; model.FlushPendingSave();
        Assert.Equal(0, wrong); Assert.Contains("ZLG", step.PropertiesSetting.General.StepDescription);
    }

    internal static void VerifyActualPage()
    {
        var step = MakeStep(); var view = new CanOpenEditorView(); view.RefreshFromStep(step);
        int commits = 0; view.ExecuteCommand = Host(step, () => { commits++; view.RefreshFromStep(step); });
        var window = new Window { Content = view, Width = 1100, Height = 700, ShowInTaskbar = false, Left = -5000, Top = -5000 };
        try
        {
            window.Show(); window.UpdateLayout();
            var adapter = Find<ComboBox>(view).First(c => c.GetBindingExpression(Selector.SelectedIndexProperty)?.ParentBinding.Path.Path == "AdapterType");
            adapter.SelectedIndex = (int)CanAdapterType.ZLG;
            Assert.Equal(1, commits);
            Assert.Contains("ZLG", step.PropertiesSetting.General.StepDescription);
            var timeout = Find<TextBox>(view).First(c => c.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "TimeoutMs");
            timeout.Text = "2500"; timeout.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.Equal(1, commits); // Text still has the 200 ms debounce.
            timeout.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, timeout));
            Assert.Equal(2, commits);
            Assert.Equal(2500, ((CanOpenSetting)new CanOpenPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, 1)).TimeoutMs);
        }
        finally { window.Close(); }
        ZlgAllHardwareTests.VerifyActualPage();
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T value) yield return value;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Find<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
