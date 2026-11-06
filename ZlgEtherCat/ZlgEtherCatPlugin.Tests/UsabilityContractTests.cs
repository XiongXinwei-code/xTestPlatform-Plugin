using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;
using Xunit;

namespace ZlgEtherCat.Tests;

[Collection("Zlg editor runtime")]
public sealed class UsabilityContractTests
{
    [Fact]
    public void GroupedDefaultsAndOverridesAreExplicit()
    {
        Assert.Equal(PdoDataType.Bytes, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo }.DataType);
        Assert.Equal(PdoDataType.Bytes, new PdoWriteSetting { Operation = PdoWriteOperation.WritePdoMasked }.DataType);
        Assert.Equal(PdoDataType.Bytes, new WriteSdoSetting().DataType);
        Assert.Equal("", new PdoReadSetting().SlaveIndexExpression);
        Assert.False(new PdoReadSetting().ReturnSample);
        Assert.Equal("", new WriteDigitalOutputSetting().ValueExpression);
        Assert.Equal("", new ReadHistorySetting().AfterSequenceExpression);
        Assert.Empty(new ReadSdoSetting { DataType = PdoDataType.UInt32, Capacity = 64 }.Validate());
        Assert.NotEmpty(new ReadSdoSetting { DataType = PdoDataType.UInt32, Capacity = 2 }.Validate());
        Assert.NotEmpty(new WriteSdoSetting { CompleteAccess = true, SubIndex = 2 }.Validate());
        Assert.NotEmpty(new ReadSdoSetting { CompleteAccess = true, DataType = PdoDataType.UInt32 }.Validate());
    }

    [Fact]
    public void AxisMappingRejectsOverlapAndExplainsItsFixedWireTypes()
    {
        Assert.NotEmpty(new BindAxisSetting { InputByteLength = 8, StatusWordOffset = 0, ActualPositionOffset = 1 }.Validate());
        Assert.NotEmpty(new BindAxisSetting { AllowedModes = "1,1" }.Validate());
        var text = new BindAxisPlugin().Description;
        Assert.Contains("0x6041:00 固定 UInt16，2 字节", text);
        Assert.Contains("0x6064:00 固定 Int32，4 字节", text);
        Assert.Contains("0x6077:00 固定 Int16，2 字节", text);
        Assert.Contains("-1=未映射", text);
        Assert.Contains("绝对字节偏移", text);
    }

    [Fact]
    public void EveryStepExplainsTargetResultAndPrerequisites()
    {
        foreach (var plugin in ZlgStepCatalog.All)
        {
            Assert.Contains("返回值：", plugin.Description);
            Assert.Contains("前置操作：", plugin.Description);
        }
        var pluginPdo = new PdoReadPlugin();
        var bytes = pluginPdo.CreateSerializer().Serialize(new PdoReadSetting { Operation = PdoReadOperation.ReadPdo, ConnectionName = "\"LineA\"", SlaveIndex = 3, Offset = 2 });
        string description = pluginPdo.GenerateDescription(bytes);
        Assert.Contains("LineA", description);
        Assert.Contains("3", description);
        Assert.Contains("相对字节偏移 2", description);
    }

    [StaFact]
    public void HexInputPersistsAndInvalidInputCannotFallBackToTheOldObjectAddress()
    {
        var plugin = new WriteSdoPlugin();
        var serializer = plugin.CreateSerializer();
        Step NewStep()
        {
            var step = new Step();
            step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.SettingVersion = 1;
            step.StepSetting.Setting = serializer.Serialize(new WriteSdoSetting());
            return step;
        }
        var first = NewStep();
        var second = NewStep();
        var view = new WriteSdoEditorView();
        view.RefreshFromStep(first);
        Layout(view);
        var box = Assert.Single(Descendants(view).OfType<TextBox>(), c => c.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "IndexText");
        box.Text = "0x6040";
        box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        view.ViewModel.Flush();
        Assert.Equal(0x6040, ((WriteSdoSetting)serializer.Deserialize(first.StepSetting.Setting, 1)).Index);
        box.Text = "abc";
        box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        view.ViewModel.Flush();
        var invalid = (WriteSdoSetting)serializer.Deserialize(first.StepSetting.Setting, 1);
        Assert.Equal(int.MinValue, invalid.Index);
        Assert.NotEmpty(invalid.Validate());
        Assert.Equal("abc", view.ViewModel.IndexText);
        view.RefreshFromStep(second);
        Assert.Equal("4096", view.ViewModel.IndexText);
        Assert.Equal(4096, ((WriteSdoSetting)serializer.Deserialize(second.StepSetting.Setting, 1)).Index);
        Assert.Equal(int.MinValue, ((WriteSdoSetting)serializer.Deserialize(first.StepSetting.Setting, 1)).Index);
    }

    [StaFact]
    public void InvalidEngineeringValuePersistsAsInvalidFiniteSetting()
    {
        var plugin = new WriteAnalogOutputPlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new WriteAnalogOutputSetting());
        var view = new WriteAnalogOutputEditorView();
        view.RefreshFromStep(step);
        view.ViewModel.ValueText = "NaN";
        view.ViewModel.Flush();
        var saved = (WriteAnalogOutputSetting)serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.True(double.IsFinite(saved.Value));
        Assert.NotEmpty(saved.Validate());
        Assert.Equal("NaN", view.ViewModel.ValueText);
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(900, 700));
        view.Arrange(new Rect(0, 0, 900, 700));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject element)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
