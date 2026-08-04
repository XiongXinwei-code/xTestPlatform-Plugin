using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 批量写入插件，一次执行多个地址段的写入操作
/// </summary>
public sealed class ModbusBatchWritePlugin : StepPluginBase<ModbusBatchWriteSetting>
{
	public override string StepTypeId => "IO.ModbusBatchWrite";
	public override string DisplayName => "Modbus_BatchWrite";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"批量写入多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式。" +
		"Setting 字段：ConnectionName(string,表达式,已建立的Modbus连接名), " +
		"Items(集合,写入项列表,每个元素结构见下方JSON示例), IntervalMs(int,每项写入间隔ms,默认0)。" +
		"Items 元素JSON示例: {\"SlaveAddress\":1,\"RegisterType\":\"HoldingRegister\",\"StartAddress\":100,\"Values\":\"500,600\",\"DataFormat\":\"UInt16\"} " +
		"RegisterType可选值: Coil, HoldingRegister(写入只支持这两种)。" +
		"DataFormat可选值: UInt16, Int16, UInt32_AB_CD, Int32_AB_CD, Float_AB_CD, UInt32_CD_AB, Int32_CD_AB, Float_CD_AB。";

	public override IStepExecutor CreateExecutor() => new ModbusBatchWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchWrite {s.ConnectionName} ({s.Items.Count} items)";
	}
}