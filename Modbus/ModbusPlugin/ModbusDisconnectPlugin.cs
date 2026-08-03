using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 断开连接插件，关闭并释放指定的 Modbus 连接资源
/// </summary>
public sealed class ModbusDisconnectPlugin : StepPluginBase<ModbusDisconnectSetting>
{
	public override string StepTypeId => "IO.ModbusDisconnect";
	public override string DisplayName => "Modbus_Disconnect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description =>
		"关闭指定的 Modbus 连接。" +
		"Setting 字段：ConnectionName(string,表达式,连接标识名)。";

	public override IStepExecutor CreateExecutor() => new ModbusDisconnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Disconnect {s.ConnectionName}";
	}
}