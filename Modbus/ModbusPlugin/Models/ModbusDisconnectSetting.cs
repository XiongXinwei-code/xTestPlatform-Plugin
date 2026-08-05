using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// Modbus 断开连接步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class ModbusDisconnectSetting
{
	/// <summary>要断开的连接名称</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "\"Modbus1\"";
}