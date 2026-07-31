using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusBatchWritePlugin : StepPluginBase<ModbusBatchWriteSetting>
{
	public override string StepTypeId => "IO.ModbusBatchWrite";
	public override string DisplayName => "Modbus_BatchWrite";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"鎵归噺鍐欏叆澶氫釜 Modbus 鍦板潃娈点€? +
		"Setting 瀛楁锛欳onnectionName(string,琛ㄨ揪寮?, Items(鍒楄〃,姣忛」鍚玈laveAddress/RegisterType/StartAddress/Values/DataFormat), IntervalMs(int)銆?;

	public override IStepExecutor CreateExecutor() => new ModbusBatchWriteExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchWrite {s.ConnectionName} ({s.Items.Count} items)";
	}
}
