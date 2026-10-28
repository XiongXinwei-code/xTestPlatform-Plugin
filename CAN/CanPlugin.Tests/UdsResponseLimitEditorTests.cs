using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class UdsResponseLimitEditorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidLimitIsRejectedBeforeResolvingOrTouchingConnection(int bytes)
    {
        var result = await CAN.UDS.UdsExecutorHelper.CreateClientAsync(new UdsCommonSetting { MaxResponseBytes = bytes }, null!, default);
        Assert.Null(result.client);
        Assert.Contains("MaxResponseBytes", result.error);
    }

    internal void EveryUdsViewExposesAndPersistsResponseLimit()
    {
        // Syncfusion caches thread-affine styles: all these real views belong to one UI thread.
        foreach (var name in new[] { "UdsDiagSession", "UdsClearDtc", "UdsRawRequest", "UdsReadDataById",
                     "UdsReadDtc", "UdsRoutineControl", "UdsSecurityAccess", "UdsWriteDataById", "CanFlash" })
            VerifyView(name);
    }

    private static void VerifyView(string name)
    {
        string ns = name == "CanFlash" ? "CAN.Flash" : "CAN.UDS";
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CAN.CanOpenPlugin).Assembly.GetType($"{ns}.{name}Plugin")!)!;
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = (UserControl)Activator.CreateInstance(typeof(CAN.UI.Views.CanOpenEditorView).Assembly.GetType($"CAN.UI.Views.{name}EditorView")!)!;
        var refresh = view.GetType().GetMethod("RefreshFromStep")!;
        refresh.Invoke(view, [step]);
        view.Measure(new Size(900, 850)); view.Arrange(new Rect(0, 0, 900, 850)); view.UpdateLayout();
        if (name == "UdsSecurityAccess")
        {
            var level = Assert.Single(Logical(view).OfType<TextBox>(), c => BindingOperations.GetBinding(c, TextBox.TextProperty)?.Path.Path == "SecurityLevel");
            Assert.Contains("1~63", level.ToolTip.ToString());
            level.Text = "64";
            Assert.True(Validation.GetHasError(level));
            level.Text = "2";
            Assert.False(Validation.GetHasError(level));
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(2, ((UdsSecurityAccessSetting)serializer.Deserialize(step.StepSetting.Setting, 1)).SecurityLevel);
        }
        var input = Assert.Single(Logical(view).OfType<TextBox>(), c =>
            BindingOperations.GetBinding(c, TextBox.TextProperty)?.Path.Path == "MaxResponseBytes");
        Assert.Equal("4194304", input.Text);
        var timeout = Assert.Single(Logical(view).OfType<TextBox>(), c =>
            BindingOperations.GetBinding(c, TextBox.TextProperty)?.Path.Path == "TransactionTimeoutMs");
        Assert.Equal("60000", timeout.Text);
        timeout.Text = "12345";
        BindingOperations.GetBindingExpression(timeout, TextBox.TextProperty)!.UpdateSource();
        input.Text = "8388608";
        BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
        refresh.Invoke(view, [step]);
        Assert.Equal(8388608, ((UdsCommonSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).MaxResponseBytes);
        Assert.Equal(12345, ((UdsCommonSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).TransactionTimeoutMs);
        input.Text = "0";
        BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
        Assert.True(Validation.GetHasError(input));
        input.Text = "6291456";
        BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(6291456, ((UdsCommonSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).MaxResponseBytes);
    }

    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Logical(child)) yield return item;
    }
}
