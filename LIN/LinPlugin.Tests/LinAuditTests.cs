using System.Reflection;
using LIN.Adapters;
using LIN.Adapters.NiXnet;
using LIN.Executors;
using LIN.Models;
using LIN.Runtime;
using LIN.UI.ViewModels;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace LIN.Tests;

public sealed class LinAuditTests
{
    internal sealed class Driver : NiLinIo
    {
        uint next;
        public List<uint> Clears = new();
        public bool FailClear = true;
        public bool FailStop;
        public int Created => (int)next;
        public override int Create(string a, string b, string c, string d, uint m, out uint s) { s = ++next; return 0; }
        public override int Set(uint s, uint p, uint z, ref uint v) => 0;
        public override int Start(uint s, uint p) => 0;
        public override int Stop(uint s, uint p) => FailStop ? -2 : 0;
        public override int Clear(uint s) { Clears.Add(s); return FailClear ? -1 : 0; }
        public override void Check(int status, string c = "") { if (status < 0) throw new InvalidOperationException($"{c} native {status}"); }
    }

    [Fact]
    public void DriverClearFailureRetainsSessionsForRetry()
    {
        var io = new Driver(); var adapter = new NiXnetLinAdapter(io);
        adapter.Open(new() { Channel = "LIN1" });
        Assert.ThrowsAny<Exception>(adapter.Close);
        io.FailClear = false;
        adapter.Close();
        Assert.Equal(4, io.Clears.Count);
    }

    [Fact]
    public async Task CyclicStopMustWaitForActualWriteExit()
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>();
        var state = (LinContext)(object)context;
        var adapter = new BlockingAdapter();
        state.Items["LIN_Adapter_LIN1"] = adapter;
        state.Items[LIN.Helpers.LinHelper.GetAdapterKey("LIN1")] = adapter;
        state.Plugin = new LinCyclicSendStartPlugin();
        state.Setting = new LinCyclicSendStartSetting { ConnectionName = "\"LIN1\"", TaskName = "\"cyclic\"", Frames = [new() { FrameId = "\"0x12\"", Data = "\"01\"", CycleTimeMs = 5, Enabled = true }] };
        try
        {
            var start = await new LinCyclicSendStartExecutor().ExecuteAsync(context);
            Assert.True(start.StepResult.Status == TestStatus.Completed, start.StepResult.Error?.Message);
            await adapter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            state.Plugin = new LinCyclicSendStopPlugin(); state.Setting = new LinCyclicSendStopSetting { TaskName = "\"cyclic\"" };
            var stop = new LinCyclicSendStopExecutor().ExecuteAsync(context);
            await Task.Delay(50);
            Assert.False(stop.IsCompleted);
            adapter.Release.Set();
            Assert.Equal(TestStatus.Completed, (await stop).StepResult.Status);
        }
        finally { adapter.Release.Set(); }
    }

    [Fact]
    public async Task CloseTimeoutRetainsNativeHandleUntilActualWriteExits()
    {
        var raw = new BlockingAdapter(); var resource = new LinConnectionResource(raw);
        var job = resource.StartJob([ct => Task.Run(() => resource.Write(new() { Data = [1] }))]);
        try
        {
            await raw.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => job.StopAsync(30));
            await Assert.ThrowsAsync<TimeoutException>(() => resource.CloseAsync(30));
            Assert.Equal(0, raw.CloseCount);
            Assert.False(resource.IsClosed);
            Assert.Throws<InvalidOperationException>(() => resource.StartJob([]));
        }
        finally { raw.Release.Set(); }
        await resource.CloseAsync();
        Assert.True(resource.IsClosed); Assert.Equal(1, raw.CloseCount);
    }

    [Fact]
    public async Task WorkerFailureIsReportedByStopAndCloseAfterSafeRelease()
    {
        var raw = new BlockingAdapter { WriteFailure = new InvalidOperationException("driver write failed") };
        var resource = new LinConnectionResource(raw);
        var job = resource.StartJob([ct => Task.Run(() => resource.Write(new() { Data = [1] }))]);
        await job.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => job.StopAsync());
        await Assert.ThrowsAsync<AggregateException>(() => resource.CloseAsync());
        Assert.True(resource.IsClosed); Assert.Equal(1, raw.CloseCount);
    }

    [Fact]
    public async Task StopDriverErrorAfterSuccessfulClearIsReportedButResourceIsReleased()
    {
        var driver = new Driver { FailClear = false, FailStop = true };
        var resource = new LinConnectionResource(new NiXnetLinAdapter(driver));
        resource.Open(new() { Channel = "LIN1" });
        await Assert.ThrowsAsync<AggregateException>(() => resource.CloseAsync());
        Assert.True(resource.IsClosed); Assert.Equal(2, driver.Clears.Count);
    }

    [Theory]
    [InlineData(LinVersionType.LIN_1x, true)]
    [InlineData(LinVersionType.LIN_2x, false)]
    public void UnsupportedConfigurationFailsBeforeOpeningDriver(LinVersionType version, bool master)
    {
        var driver = new Driver(); var adapter = new NiXnetLinAdapter(driver);
        Assert.Throws<NotSupportedException>(() => adapter.Open(new() { LinVersion = version, IsMaster = master }));
        Assert.Equal(0, driver.Created);
    }

    [Fact]
    public void ClassicChecksumIsNeverSilentlyIgnored()
    {
        var driver = new Driver { FailClear = false }; using var adapter = new NiXnetLinAdapter(driver);
        adapter.Open(new() { Channel = "LIN1" });
        Assert.Throws<NotSupportedException>(() => adapter.Write(new() { FrameId = 0x12, Data = [1], ChecksumType = LinChecksumType.Classic }));
    }

    internal void EditorCollectionChangesPersistBeforeRefresh()
    {
        Round2EditorContractTests.ActualDeleteRemovesSelectedRow();
        var plugin = new LinCyclicSendStartPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new LinCyclicSendStartSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new LinCyclicSendStartViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.AddFrame(); vm.Frames[^1].FrameId = "\"0x21\"";
        vm.AttachStep(step);
        Assert.Equal("\"0x21\"", vm.Frames[^1].FrameId);
        var count = vm.Frames.Count; vm.RemoveFrame(vm.Frames[^1]); vm.AttachStep(step);
        Assert.Equal(count - 1, vm.Frames.Count);
        vm.AddFrame(); var stale = vm.Frames[^1]; vm.Flush();
        var other = new Step(); other.StepSetting.Setting = serializer.Serialize(new LinCyclicSendStartSetting()); other.StepSetting.SettingVersion = serializer.SettingVersion;
        vm.AttachStep(other); var bytes = other.StepSetting.Setting;
        stale.FrameId = "\"0x22\""; vm.Flush();
        Assert.Same(bytes, other.StepSetting.Setting);
        vm.Frames.Add(new LinCyclicFrameItem { FrameId = "\"0x23\"" }); vm.Flush();
        Assert.Contains(((LinCyclicSendStartSetting)serializer.Deserialize(other.StepSetting.Setting, serializer.SettingVersion)).Frames, f => f.FrameId == "\"0x23\"");
    }

    [StaFact]
    public void ActualOpenViewLoadsCapabilityHintAndPersistsBoundControl()
    {
        EditorCollectionChangesPersistBeforeRefresh(); // Syncfusion 实际视图在同一 STA 生命周期验证。
        var plugin = new LinOpenPlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new LinOpenSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new LIN.UI.Views.LinOpenEditorView(); view.RefreshFromStep(step);
        view.Measure(new System.Windows.Size(800, 600)); view.Arrange(new System.Windows.Rect(0, 0, 800, 600)); view.UpdateLayout();
        var controls = Logical(view).ToArray();
        var versions = controls.OfType<System.Windows.Controls.ComboBox>().Single(c => c.Items.OfType<System.Windows.Controls.ComboBoxItem>().Any(i => i.Content?.ToString()?.StartsWith("LIN 1.x") == true));
        Assert.False(((System.Windows.Controls.ComboBoxItem)versions.Items[0]).IsEnabled);
        Assert.Contains(controls.OfType<System.Windows.Controls.Grid>(), g => g.ToolTip?.ToString()?.Contains("无 LDF") == true);
        var master = Assert.Single(controls.OfType<System.Windows.Controls.CheckBox>());
        master.IsChecked = false;
        System.Windows.Data.BindingOperations.GetBindingExpression(master, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.UpdateSource();
        view.RefreshFromStep(step);
        Assert.False(view.ViewModel.IsMaster);
        Assert.True(view.ActualHeight > 0);
        var timeout = Assert.Single(Logical(view).OfType<System.Windows.Controls.TextBox>(), c =>
            System.Windows.Data.BindingOperations.GetBinding(c, System.Windows.Controls.TextBox.TextProperty)?.Path.Path == "TimeoutMs");
        timeout.Text = "7500";
        System.Windows.Data.BindingOperations.GetBindingExpression(timeout, System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
        view.RefreshFromStep(step);
        Assert.Equal(7500, ((LinOpenSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).TimeoutMs);
        var closeSerializer = new LinClosePlugin().CreateSerializer();
        step.StepSetting.Setting = closeSerializer.Serialize(new LinCloseSetting()); step.StepSetting.SettingVersion = closeSerializer.SettingVersion;
        var closeView = new LIN.UI.Views.LinCloseEditorView(); closeView.RefreshFromStep(step);
        closeView.Measure(new System.Windows.Size(800, 500)); closeView.Arrange(new System.Windows.Rect(0, 0, 800, 500)); closeView.UpdateLayout();
        var closeTimeout = Assert.Single(Logical(closeView).OfType<System.Windows.Controls.TextBox>(), c =>
            System.Windows.Data.BindingOperations.GetBinding(c, System.Windows.Controls.TextBox.TextProperty)?.Path.Path == "TimeoutMs");
        closeTimeout.Text = "1200";
        System.Windows.Data.BindingOperations.GetBindingExpression(closeTimeout, System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
        closeView.RefreshFromStep(step);
        Assert.Equal(1200, ((LinCloseSetting)closeSerializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).TimeoutMs);
        closeTimeout.Text = "1800";
        System.Windows.Data.BindingOperations.GetBindingExpression(closeTimeout, System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
        closeView.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        Assert.Equal(1800, ((LinCloseSetting)closeSerializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)).TimeoutMs);
        foreach (string name in new[] { "LinWrite", "LinWakeup", "LinSleep", "LinCyclicSendStart", "LinCyclicSendStop", "LinWriteRead" })
        {
            string field = name == "LinCyclicSendStart" ? "ReplaceTimeoutMs" : name == "LinWriteRead" ? "SendTimeoutMs" : "TimeoutMs";
            var actionPlugin = (IStepPlugin)Activator.CreateInstance(typeof(LinOpenPlugin).Assembly.GetType($"LIN.{name}Plugin")!)!;
            var actionSerializer = actionPlugin.CreateSerializer();
            var actionStep = new Step(); actionStep.StepSetting.Setting = actionSerializer.Serialize(actionSerializer.CreateDefault()); actionStep.StepSetting.SettingVersion = actionSerializer.SettingVersion;
            var actionView = (System.Windows.Controls.UserControl)Activator.CreateInstance(typeof(LIN.UI.Views.LinOpenEditorView).Assembly.GetType($"LIN.UI.Views.{name}EditorView")!)!;
            actionView.GetType().GetMethod("RefreshFromStep")!.Invoke(actionView, [actionStep]);
            actionView.Measure(new System.Windows.Size(800, 600)); actionView.Arrange(new System.Windows.Rect(0, 0, 800, 600)); actionView.UpdateLayout();
            var input = Assert.Single(Logical(actionView).OfType<System.Windows.Controls.TextBox>(), c => System.Windows.Data.BindingOperations.GetBinding(c, System.Windows.Controls.TextBox.TextProperty)?.Path.Path == field);
            Assert.Equal("3000", input.Text); input.Text = "777";
            System.Windows.Data.BindingOperations.GetBindingExpression(input, System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
            actionView.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
            Assert.Equal(777, actionSerializer.Deserialize(actionStep.StepSetting.Setting, actionStep.StepSetting.SettingVersion).GetType().GetProperty(field)!.GetValue(actionSerializer.Deserialize(actionStep.StepSetting.Setting, actionStep.StepSetting.SettingVersion)));
        }
    }

    private static IEnumerable<System.Windows.DependencyObject> Logical(System.Windows.DependencyObject root)
    {
        yield return root;
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<System.Windows.DependencyObject>())
            foreach (var item in Logical(child)) yield return item;
    }

    [Fact]
    public async Task ClosingConnectionRemovesItsFailedJobRegistrationOnlyAfterRelease()
    {
        var context = DispatchProxy.Create<IExecutionContext, LinContext>(); var state = (LinContext)(object)context;
        var raw = new BlockingAdapter { WriteFailure = new InvalidOperationException("driver failed") };
        var connection = new LinConnectionResource(raw);
        var job = connection.StartJob([ct => Task.Run(() => connection.Write(new() { Data = [1] }))]);
        job.RegistrationKey = "LIN_CyclicSend_failed";
        state.Items[LIN.Helpers.LinHelper.GetAdapterKey("LIN1")] = connection;
        state.Items[job.RegistrationKey] = job;
        await job.Completion;
        state.Plugin = new LinClosePlugin(); state.Setting = new LinCloseSetting { ConnectionName = "\"LIN1\"" };
        var result = await new LinCloseExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.True(connection.IsClosed);
        Assert.DoesNotContain(state.Items, item => !item.Key.StartsWith("PluginCommon.ExecutionEpoch:", StringComparison.Ordinal));
    }
}

public sealed class BlockingAdapter : ILinAdapter
{
    public readonly ManualResetEventSlim Release = new();
    public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int CloseCount;
    public Exception? WriteFailure;
    public bool IsConnected => true;
    public void Open(LinAdapterConfig c) { }
    public void Close() { CloseCount++; }
    public void Write(LinFrame f) { Entered.TrySetResult(); if (WriteFailure != null) throw WriteFailure; Release.Wait(); }
    public LinFrame? Read(int t, CancellationToken c = default) => null;
    public LinFrame? Read(byte id, int t, CancellationToken c = default) => null;
    public void Wakeup(bool r = true) { }
    public void Sleep(bool r = true) { }
    public void Dispose() { }
}

public class LinContext : DispatchProxy
{
    public readonly Dictionary<string, object> Items = new();
    public IStepPlugin Plugin = new LinClosePlugin();
    public object Setting = new LinCloseSetting();
    public byte[]? PayloadOverride;
    public Action<string>? OnLog;
    object? registry;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "Log") OnLog?.Invoke(string.Join(" ", args ?? []));
        if (method!.ReturnType == typeof(void)) return null;
        if (method!.Name == "get_CurrentStep")
        {
            var step = new Step(); var serializer = Plugin.CreateSerializer();
            step.StepSetting.StepType = Plugin.StepTypeId; step.StepSetting.SettingVersion = serializer.SettingVersion; step.StepSetting.Setting = PayloadOverride ?? serializer.Serialize(Setting);
            return new StepExecutionInfo { Step = step };
        }
        if (method.Name == "get_Resources")
        {
            if (registry == null) { registry = Create(method.ReturnType, typeof(LinRegistry)); ((LinRegistry)registry).Owner = this; }
            return registry;
        }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
public class LinRegistry : DispatchProxy
{
    public LinContext Owner = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet") { var ok = Owner.Items.TryGetValue((string)args![0]!, out var v) && method.GetGenericArguments()[0].IsInstanceOfType(v); args[1] = ok ? v : null; return ok; }
        if (method.Name == "Contains") return Owner.Items.ContainsKey((string)args![0]!);
        if (method.Name == "TryAdd") return Owner.Items.TryAdd((string)args![0]!, args[1]!);
        if (method.Name == "Set") { if (Owner.Items.TryGetValue((string)args![0]!, out var old) && !ReferenceEquals(old, args[1])) (old as IDisposable)?.Dispose(); Owner.Items[(string)args[0]!] = args[1]!; return null; }
        if (method.Name == "Remove") { bool removed = Owner.Items.Remove((string)args![0]!, out var old); (old as IDisposable)?.Dispose(); return removed; }
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
