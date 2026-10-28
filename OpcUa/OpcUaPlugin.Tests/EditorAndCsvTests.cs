using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.ViewModels;
using OpcUa.UI.Views;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace OpcUa.Tests;

public sealed class EditorAndCsvTests
{
    [StaFact]
    public void RefreshImmediatelyAfterEditPersistsOriginalStep()
    {
        var plugin = new OpcUaConnectPlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new OpcUaConnectSetting());
        var vm = new OpcUaConnectViewModel();
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.EndpointUrl = "\"opc.tcp://changed:4840\"";
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        Assert.Equal("\"opc.tcp://changed:4840\"", vm.EndpointUrl);
    }

    [StaFact]
    public void ExistingAcquisitionRowEditSurvivesRefresh()
    {
        var serializer = new OpcUaDataAcqStartPlugin().CreateSerializer();
        var setting = new OpcUaDataAcqStartSetting();
        setting.Items.Add(new() { NodeId = "ns=2;s=Old", ColumnName = "Old" });
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(setting);
        var vm = new OpcUaDataAcqStartViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.Items[0].NodeId = "ns=2;s=Changed";
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        Assert.Equal("ns=2;s=Changed", Assert.Single(vm.Items).NodeId);
    }

    [Fact]
    public void CsvEscapesColumnAndValueWithoutCorruptingRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opcua-audit-{Guid.NewGuid():N}.csv");
        try
        {
            OpcUaDataAcqTask.AppendCsv(path,
                [new() { NodeId = "ns=2;s=V", ColumnName = "a,b\"c" }],
                [new() { Timestamp = DateTime.UtcNow, Values = ["x,y\"z\nsecond"] }]);
            var content = File.ReadAllText(path);
            Assert.Contains("\"a,b\"\"c\"", content);
            Assert.Contains("\"x,y\"\"z\nsecond\"", content);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void CsvWithMultilineQuotedHeaderCanBeAppendedAgain()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opcua-audit-{Guid.NewGuid():N}.csv");
        try
        {
            var items = new OpcUaDataAcqItem[] { new() { NodeId = "ns=2;s=V", ColumnName = "multi\nline" } };
            var records = new List<DataAcqRecord> { new() { Timestamp = DateTime.UtcNow, Values = [5] } };
            OpcUaDataAcqTask.AppendCsv(path, items, records);
            OpcUaDataAcqTask.AppendCsv(path, items, records);
            Assert.Equal(2, File.ReadAllText(path).Split("Unknown").Length - 1);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ActualAcquisitionEditorRemovesSelectedRowAndFlushesOnUnload() => UiThread.Run(() =>
    {
        var serializer = new OpcUaDataAcqStartPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new OpcUaDataAcqStartSetting());
        var view = new OpcUaDataAcqStartEditorView(); view.RefreshFromStep(step);
        view.ViewModel.AddItem(); view.ViewModel.Items[0].NodeId = "ns=2;s=V";
        view.Measure(new Size(900, 500)); view.Arrange(new Rect(0, 0, 900, 500)); view.UpdateLayout();
        var grid = Assert.IsType<DataGrid>(view.FindName("ItemsGrid"));
        grid.SelectedItem = view.ViewModel.Items[0];
        typeof(OpcUaDataAcqStartEditorView).GetMethod("OnRemoveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, [new Button { DataContext = view.ViewModel }, new RoutedEventArgs(Button.ClickEvent)]);
        Assert.Empty(view.ViewModel.Items);
        view.ViewModel.SamplingIntervalMs = 123;
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (OpcUaDataAcqStartSetting)serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(123, saved.SamplingIntervalMs);
        Assert.Empty(saved.Items);
    });

    [Fact]
    public void ActualReadEditorFlushesPendingChangeOnUnload() => UiThread.Run(() =>
    {
        var serializer = new OpcUaReadPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new OpcUaReadSetting());
        var view = new OpcUaReadEditorView(); view.RefreshFromStep(step);
        view.ViewModel.TimeoutMs = 1234;
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(1234, ((OpcUaReadSetting)serializer.Deserialize(step.StepSetting.Setting, 1)).TimeoutMs);
    });
}
