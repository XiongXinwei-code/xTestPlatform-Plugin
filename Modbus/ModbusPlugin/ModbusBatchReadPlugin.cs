using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 批量读取插件，一次执行多个地址段的读取操作
/// </summary>
public sealed class ModbusBatchReadPlugin : StepPluginBase<ModbusBatchReadSetting>
{
	public override string StepTypeId => "IO.ModbusBatchRead";
	public override string DisplayName => "Modbus_BatchRead";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"批量读取多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式，每个项的读取结果分别存入对应变量。" +
		"Setting 字段：ConnectionName(string,表达式,已建立的Modbus连接名), " +
		"Items(集合,读取项列表,每个元素结构见下方JSON示例), IntervalMs(int,每项读取间隔ms,默认0)。" +
		"Items 元素JSON示例: {\"SlaveAddress\":1,\"RegisterType\":\"HoldingRegister\",\"StartAddress\":0,\"Quantity\":2,\"DataFormat\":\"Float_AB_CD\",\"ResultVariable\":\"temperature\"} " +
		"RegisterType可选值: Coil, DiscreteInput, HoldingRegister, InputRegister。" +
		"DataFormat可选值: UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB。";

	public override IStepExecutor CreateExecutor() => new ModbusBatchReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchRead {s.ConnectionName} ({s.Items.Count} items)";
	}
}