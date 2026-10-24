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

    /// <summary>关闭总期限(ms，0=连接配置)，包含旧资源收尾及本次操作；旧序列保留原默认行为。</summary>
    public int TimeoutMs { get; set; } = 0;
}
