using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

public sealed class ModbusBatchReadPlugin : StepPluginBase<ModbusBatchReadSetting>
{
	public override string StepTypeId => "IO.ModbusBatchRead";
	public override string DisplayName => "Modbus_BatchRead";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"鎵归噺璇诲彇澶氫釜 Modbus 鍦板潃娈碉紝姣忎釜椤瑰彲鎸囧畾涓嶅悓浠庣珯銆佸瘎瀛樺櫒绫诲瀷鍜屾暟鎹牸寮忋€? +
		"Setting 瀛楁锛欳onnectionName(string,琛ㄨ揪寮?, Items(鍒楄〃,姣忛」鍚玈laveAddress/RegisterType/StartAddress/Quantity/DataFormat/ResultVariable), IntervalMs(int)銆?;

	public override IStepExecutor CreateExecutor() => new ModbusBatchReadExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"BatchRead {s.ConnectionName} ({s.Items.Count} items)";
	}
}
