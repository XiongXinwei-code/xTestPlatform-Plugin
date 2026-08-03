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
		"批量写入多个 Modbus 地址段。" +
		"Setting 字段：ConnectionName(string,表达式), Items(列表,每项含 SlaveAddress/RegisterType/StartAddress/Values/DataFormat), IntervalMs(int,写入间隔)。";

	public override IStepExecutor CreateExecutor() => new ModbusBatchWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchWrite {s.ConnectionName} ({s.Items.Count} items)";
	}
}