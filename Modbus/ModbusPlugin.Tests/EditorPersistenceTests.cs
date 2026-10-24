using Modbus.Models;
using Modbus.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;
namespace Modbus.ContractTests;
public class EditorPersistenceTests
{
    [Fact]
    public void WritePageLoadsAndLaysOutWithoutADevice() => UiThread.Run(() =>
    {
        var page = new Modbus.UI.Views.ModbusWriteEditorView();
        page.Measure(new System.Windows.Size(1000, 700)); page.Arrange(new System.Windows.Rect(0, 0, 1000, 700));
        Assert.True(page.IsMeasureValid);
    });
    [StaFact]
    public void HoldingSelectionPersistsTheExistingEnumValueBeforeRefresh()
    {
        var serializer = new ModbusWritePlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new ModbusWriteSetting { RegisterType = ModbusRegisterType.Coil });
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new ModbusWriteViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.RegisterType = 1; vm.AttachStep(step);
        var saved = (ModbusWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal(ModbusRegisterType.HoldingRegister, saved.RegisterType);
    }
    [StaFact]
    public void DirectCollectionAndItemEditsPersistWithoutAnotherFieldChange()
    {
        var serializer = new ModbusBatchWritePlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new ModbusBatchWriteSetting()); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var vm = new ModbusBatchWriteViewModel(); vm.AttachSerializer(serializer); vm.AttachStep(step);
        vm.Items.Clear(); vm.Items.Add(new ModbusBatchWriteItem { Values = "1" }); vm.Items[0].Values = "5";
        vm.AttachStep(step); // 刷新必须先 Flush，不能依赖另一个字段偶然触发保存。
        var saved = (ModbusBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("5", Assert.Single(saved.Items).Values);
    }
}
