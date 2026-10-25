using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.CANopen;
using CAN.CANopen.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

internal static class FollowupEditorInputTests
{
    // The existing single-STA actual-view test calls this helper. Do not create
    // another Syncfusion STA or pump an unregistered third-party license dialog.
    internal static void VerifySdoPathBeforeRefreshAndUnload()
    {
        var failures = new List<string>();
        foreach (bool write in new[] { false, true })
        foreach (string transition in new[] { "unload", "refresh", "switch" })
        {
            IStepPlugin plugin = write ? new CanopenSdoWritePlugin() : new CanopenSdoReadPlugin();
            IStepEditorPlugin editor = write ? new CAN.UI.CanopenSdoWriteEditorPlugin() : new CAN.UI.CanopenSdoReadEditorPlugin();
            var serializer = plugin.CreateSerializer();
            Step CreateStep(string path)
            {
                var setting = (CanopenSdoSettingBase)serializer.CreateDefault(); setting.EdsFilePath = path;
                var step = new Step(); step.StepSetting.Setting = serializer.Serialize(setting);
                step.StepSetting.SettingVersion = serializer.SettingVersion; return step;
            }
            var first = CreateStep("previous.eds"); var next = CreateStep("next-step.eds");
            var view = editor.CreateEditor(first, new SequenceFile());
            view.Measure(new Size(900, 850)); view.Arrange(new Rect(0, 0, 900, 850)); view.UpdateLayout();
            var input = Logical(view).OfType<TextBox>().Single(text =>
                BindingOperations.GetBinding(text, TextBox.TextProperty)?.Path.Path == "EdsFilePath");
            input.Text = "typed-without-losing-focus.eds";
            // No artificial UpdateSource or LostFocus: the page owns the flush.
            if (transition == "unload") view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            else ((IRefreshableEditor)view).RefreshFromStep(transition == "switch" ? next : first);
            var saved = (CanopenSdoSettingBase)serializer.Deserialize(first.StepSetting.Setting, serializer.SettingVersion);
            var other = (CanopenSdoSettingBase)serializer.Deserialize(next.StepSetting.Setting, serializer.SettingVersion);
            if (saved.EdsFilePath != "typed-without-losing-focus.eds") failures.Add($"{plugin.StepTypeId}/{transition}: 原步骤丢失未失焦路径");
            if (other.EdsFilePath != "next-step.eds") failures.Add($"{plugin.StepTypeId}/{transition}: 旧输入写到新步骤");
            if (transition == "refresh" && input.Text != "typed-without-losing-focus.eds") failures.Add($"{plugin.StepTypeId}: 刷新重新显示了旧路径");
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var nested in Logical(child)) yield return nested;
    }
}
