using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchReadSetting
{
	/// <summary>杩炴帴鏍囪瘑鍚?/summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>鎵归噺璇诲彇椤瑰垪琛?/summary>
	public List<ModbusBatchItem> Items { get; set; } = new();

	/// <summary>璇诲彇闂撮殧(ms)锛?=鏃犻棿闅?/summary>
	public int IntervalMs { get; set; } = 0;
}
