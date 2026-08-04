using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

/// <summary>
/// Modbus 批量读取步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class ModbusBatchReadSetting
{
	/// <summary>使用的连接名称</summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>批量读取项列表</summary>
	public ObservableCollection<ModbusBatchItem> Items { get; set; } = new();

	/// <summary>每次读取之间的间隔时间（毫秒），0 表示无间隔</summary>
	public int IntervalMs { get; set; } = 0;
}