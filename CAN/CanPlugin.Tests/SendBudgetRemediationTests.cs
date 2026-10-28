using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.Models;
using CAN.Helpers;
using CAN.Runtime;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class SendBudgetRemediationTests
{
    public static IEnumerable<object[]> Settings()
    {
        yield return [new CanopenNmtControlPlugin(), "SendTimeoutMs", 1000];
        yield return [new CanopenPdoSendPlugin(), "SendTimeoutMs", 1000];
        yield return [new CanopenSyncSendPlugin(), "SendTimeoutMs", 1000];
        yield return [new CanCyclicSendStopPlugin(), "TimeoutMs", 5000];
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void User_budget_roundtrips_and_old_empty_map_keeps_default(IStepPlugin plugin, string property, int defaultValue)
    {
        var serializer = plugin.CreateSerializer(); var setting = serializer.CreateDefault();
        var field = setting.GetType().GetProperty(property);
        Assert.NotNull(field);
        Assert.Equal(defaultValue, field.GetValue(serializer.Deserialize([0x80], 1)));
        field.SetValue(setting, 83);
        Assert.Equal(83, field.GetValue(serializer.Deserialize(serializer.Serialize(setting), 1)));
        Assert.Contains(property, plugin.Description);
    }

    [Theory]
    [InlineData("CanopenNmtControl")]
    [InlineData("CanopenPdoSend")]
    [InlineData("CanopenSyncSend")]
    public async Task Simple_send_steps_use_configured_budget_without_freeing_pending_io(string name)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanopenNmtControlPlugin).Assembly.GetType($"CAN.CANopen.{name}Plugin")!)!;
        var setting = plugin.CreateSerializer().CreateDefault(); setting.GetType().GetProperty("SendTimeoutMs")!.SetValue(setting, 100);
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = plugin; state.Setting = setting;
        await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"CAN1\"", context);
        using var release = new ManualResetEventSlim(); var raw = new FakeAdapter { WriteBlock = release };
        var owner = new CanConnectionResource(raw); state.Adapter = owner;
        var call = plugin.CreateExecutor().ExecuteAsync(context);
        try
        {
            await raw.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var result = await call.WaitAsync(TimeSpan.FromMilliseconds(800));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, raw.CloseCount); Assert.False(owner.IsClosed);
            await Assert.ThrowsAsync<TimeoutException>(() => owner.CloseAsync(30));
        }
        finally { release.Set(); await call; await owner.CloseAsync(); }
    }

    internal void ActualViewsPersistConfiguredBudgets()
    {
        foreach (var entry in Settings())
        {
            var plugin = (IStepPlugin)entry[0]; var property = (string)entry[1];
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = 1;
            var name = plugin.GetType().Name.Replace("Plugin", "EditorView");
            var view = (UserControl)Activator.CreateInstance(typeof(CAN.UI.Views.CanOpenEditorView).Assembly.GetType("CAN.UI.Views." + name)!)!;
            var refresh = view.GetType().GetMethod("RefreshFromStep")!; refresh.Invoke(view, [step]);
            view.Measure(new Size(900, 700)); view.Arrange(new Rect(0, 0, 900, 700)); view.UpdateLayout();
            var input = Assert.Single(Logical(view).OfType<TextBox>(), t => BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path.Path == property);
            input.Text = "173"; BindingOperations.GetBindingExpression(input, TextBox.TextProperty)!.UpdateSource();
            refresh.Invoke(view, [step]);
            var saved = serializer.Deserialize(step.StepSetting.Setting, 1);
            Assert.Equal(173, saved.GetType().GetProperty(property)!.GetValue(saved));
        }
    }

    [Fact]
    public async Task Generic_cycle_stop_uses_user_budget_and_keeps_unexited_job()
    {
        var plugin = new CanCyclicSendStopPlugin();
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = plugin; state.Setting = new CanCyclicSendStopSetting { TaskName = "\"work\"", TimeoutMs = 83 };
        await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"CAN1\"", context);
        var owner = new CanConnectionResource(new FakeAdapter()); state.Adapter = owner;
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = owner.StartJob("work", _ => exited.Task);
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromMilliseconds(900));
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.False(job.Completion.IsCompleted);
            Assert.False(owner.IsClosed);
        }
        finally { exited.TrySetResult(); await owner.CloseAsync(); }
    }

    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var nested in Logical(child)) yield return nested;
    }
}
