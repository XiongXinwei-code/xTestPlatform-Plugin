using Opc.Ua;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.ViewModels;
using OpcUa.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace OpcUa.Tests;

public sealed class OutputTests
{
    [Fact]
    public void WaveformUsesRecordIndexNotFabricatedTimeAndRejectsBadQuality()
    {
        var records = new DataAcqRecord[]
        {
            new() { Sequence = 7, Timestamp = DateTime.UtcNow, Values = [10], StatusCodes = [StatusCodes.Good] },
            new() { Sequence = 8, Timestamp = DateTime.UtcNow.AddSeconds(7), Values = [999], StatusCodes = [StatusCodes.BadNoCommunication] }
        };
        var wave = OpcUaDataAcqOutput.CreateWaveform("test", [new() { NodeId = "ns=2;s=V" }], records);
        Assert.Equal("sample", wave.XUnit); Assert.Equal(1, wave.SampleRate); Assert.Equal(7, wave.X0);
        var channel = Assert.Single(wave.Channels);
        Assert.Equal(10, channel.Values[0]); Assert.True(double.IsNaN(channel.Values[1]));
        Assert.Equal(999, records[1].Values[0]);
    }

    [StaFact]
    public void MetadataAndHealthVariableConfigurationSurvivesRefresh()
    {
        var serializer = new OpcUaDataAcqReadPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new OpcUaDataAcqReadSetting());
        var vm = new OpcUaDataAcqReadViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.RecordsJsonVariable = "Locals.RecordsJson"; vm.HealthVariable = "Locals.Health";
        vm.AttachSerializer(serializer); vm.AttachStep(step);
        Assert.Equal("Locals.RecordsJson", vm.RecordsJsonVariable); Assert.Equal("Locals.Health", vm.HealthVariable);
    }

    [Fact]
    public void ActualMetadataControlsSaveAndExposeNonUniformTimeSemantics() => UiThread.Run(() =>
    {
        var serializer = new OpcUaDataAcqReadPlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new OpcUaDataAcqReadSetting());
        var view = new OpcUaDataAcqReadEditorView(); view.RefreshFromStep(step);
        view.Measure(new Size(700, 300)); view.Arrange(new Rect(0, 0, 700, 300)); view.UpdateLayout();
        var raw = Assert.IsType<TextBox>(view.FindName("RecordsJsonVariableBox")); raw.Text = "Locals.RawRecords";
        BindingOperations.GetBindingExpression(raw, TextBox.TextProperty)!.UpdateSource();
        var health = Assert.IsType<TextBox>(view.FindName("HealthVariableBox")); health.Text = "Locals.Health";
        BindingOperations.GetBindingExpression(health, TextBox.TextProperty)!.UpdateSource();
        view.RefreshFromStep(step);
        Assert.Equal("Locals.RawRecords", view.ViewModel.RecordsJsonVariable); Assert.Equal("Locals.Health", view.ViewModel.HealthVariable);
        var hint = Assert.IsType<TextBlock>(view.FindName("SamplingTimeHint"));
        Assert.Equal(TextWrapping.Wrap, hint.TextWrapping); Assert.Contains("记录序号", hint.Text);
        Assert.Contains("SourceTimestamp", new OpcUaDataAcqReadPlugin().Description);
    });
}
