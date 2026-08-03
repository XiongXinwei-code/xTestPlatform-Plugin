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
		"批量读取多个 Modbus 地址段，每个项可指定不同从站、寄存器类型和数据格式。" +
		"Setting 字段：ConnectionName(string,表达式), Items(列表,每项含 SlaveAddress/RegisterType/StartAddress/Quantity/DataFormat/ResultVariable), IntervalMs(int,读取间隔)。";

	public override IStepExecutor CreateExecutor() => new ModbusBatchReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchRead {s.ConnectionName} ({s.Items.Count} items)";
	}
}