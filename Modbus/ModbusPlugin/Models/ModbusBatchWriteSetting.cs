using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// Modbus 批量写入步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class ModbusBatchWriteSetting
{
	/// <summary>使用的连接名称</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>批量写入项列表</summary>
	public List<ModbusBatchWriteItem> Items { get; set; } = new();

	/// <summary>每次写入之间的间隔时间（毫秒），0 表示无间隔</summary>
	public int IntervalMs { get; set; } = 0;
}