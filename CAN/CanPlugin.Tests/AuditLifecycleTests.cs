using CAN.Adapters.Kvaser;
using CAN.Adapters;
using CAN.Models;
using CAN.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class AuditLifecycleTests
{
    private sealed class Driver : KvaserIo
    {
        public int Closes;
        public bool FailParameters = true;
        public bool FailClose;
        public int FdRate; public uint FdTseg1, FdTseg2;
        public override void Initialize() { }
        public override int Open(int c, int f) => 42;
        public override int SetParameters(int h, int r, uint a, uint b, uint c) => FailParameters ? -1 : 0;
        public override int SetFdParameters(int h, int r, uint a, uint b, uint c) { FdRate = r; FdTseg1 = a; FdTseg2 = b; return 0; }
        public override int BusOn(int h) => 0;
        public override int BusOff(int h) => 0;
        public override int Close(int h) { Closes++; return FailClose ? -2 : 0; }
        public override void Check(int status) { if (status < 0) throw new InvalidOperationException($"native {status}"); }
    }

    [Fact]
    public void KvaserCustomFdSamplePointUsesExplicitPositiveRateAndSegments()
    {
        var io = new Driver { FailParameters = false }; using var adapter = new KvaserAdapter(io);
        var setting = new CanAdapterConfig { Channel = "0", Protocol = CanProtocolType.FD, DataBitRate = 2000000, DataSamplePoint = 75 };
        adapter.Open(setting);
        Assert.Equal(2000000, io.FdRate);
        Assert.Equal(setting.AppliedDataSamplePoint, 100d * (1 + io.FdTseg1) / (1 + io.FdTseg1 + io.FdTseg2));
        Assert.Equal(75, setting.AppliedDataSamplePoint);
    }

    [Fact]
    public void PartialKvaserOpenReleasesAcquiredHandle()
    {
        var io = new Driver(); var adapter = new KvaserAdapter(io);
        Assert.Throws<InvalidOperationException>(() => adapter.Open(new() { Channel = "0" }));
        adapter.Dispose();
        Assert.Equal(1, io.Closes);
    }

    [Fact]
    public void KvaserCloseFailureKeepsHandleForRetry()
    {
        var io = new Driver { FailParameters = false, FailClose = true }; var adapter = new KvaserAdapter(io);
        adapter.Open(new() { Channel = "0" });
        Assert.Throws<InvalidOperationException>(adapter.Close);
        io.FailClose = false; adapter.Close();
        Assert.Equal(2, io.Closes);
    }

    [Fact]
    public async Task FailedOpenRetainsOriginalAndCleanupErrorsUntilConfirmedClose()
    {
        var io = new Driver { FailClose = true };
        var resource = new CAN.Runtime.CanConnectionResource(new KvaserAdapter(io));
        try
        {
            var error = Assert.Throws<AggregateException>(() => resource.Open(new() { Channel = "0" }));
            Assert.Contains("native -1", error.ToString());
            Assert.Contains("native -2", error.ToString());
            Assert.False(resource.IsClosed);
        }
        finally { io.FailClose = false; await resource.CloseAsync(); }
        Assert.True(resource.IsClosed);
    }

    [StaFact]
    public void CyclicCollectionSavesOnFlushAndDetachesPreviousRows()
    {
        var serializer = new CanCyclicSendStartPlugin().CreateSerializer();
        Step Make() { var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new CanCyclicSendStartSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion; return step; }
        var first = Make(); var second = Make(); var vm = new CanCyclicSendStartViewModel();
        vm.AttachSerializer(serializer); vm.AttachStep(first); var before = first.StepSetting.Setting;
        vm.AddMessage(); var stale = vm.Messages[^1]; stale.CanId = "0x201";
        Assert.Same(before, first.StepSetting.Setting);
        vm.AttachStep(second);
        Assert.Contains(((CanCyclicSendStartSetting)serializer.Deserialize(first.StepSetting.Setting, serializer.SettingVersion)).Messages, f => f.CanId == "0x201");
        var bytes = second.StepSetting.Setting; stale.CanId = "0x301"; vm.Flush(); Assert.Same(bytes, second.StepSetting.Setting);
        vm.Messages.Add(new CyclicMessageItem { CanId = "0x401" }); vm.Flush();
        Assert.Contains(((CanCyclicSendStartSetting)serializer.Deserialize(second.StepSetting.Setting, serializer.SettingVersion)).Messages, f => f.CanId == "0x401");
    }

    [StaFact]
    public void LegacyEditorPersistsBeforeImmediateRefresh()
    {
        var plugin = new CanClosePlugin(); var serializer = plugin.CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new CanCloseSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new CanCloseViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.ConnectionName = "\"NEW\"";
        vm.AttachStep(step);
        Assert.Equal("\"NEW\"", vm.ConnectionName);
    }

    internal void ActualOpenViewSavesControlBindingBeforeRefresh()
    {
        var serializer = new CanOpenPlugin().CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new CanOpenSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new CAN.UI.Views.CanOpenEditorView(); view.RefreshFromStep(step);
        view.Measure(new System.Windows.Size(800, 700)); view.Arrange(new System.Windows.Rect(0, 0, 800, 700)); view.UpdateLayout();
        var check = Logical(view).OfType<System.Windows.Controls.CheckBox>().Single(c =>
            System.Windows.Data.BindingOperations.GetBinding(c, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.Path.Path == "EnableTermination");
        check.IsChecked = true;
        System.Windows.Data.BindingOperations.GetBindingExpression(check, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.UpdateSource();
        view.RefreshFromStep(step);
        Assert.True(view.ViewModel.EnableTermination);
        Assert.True(view.ActualHeight > 0);
    }

    private static IEnumerable<System.Windows.DependencyObject> Logical(System.Windows.DependencyObject root)
    {
        yield return root;
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<System.Windows.DependencyObject>())
            foreach (var item in Logical(child)) yield return item;
    }
}
