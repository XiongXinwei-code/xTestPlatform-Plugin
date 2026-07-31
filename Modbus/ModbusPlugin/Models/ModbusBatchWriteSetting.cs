using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Modbus.Models;

[MessagePackObject(true)]
public class ModbusBatchWriteSetting
{
	/// <summary>杩炴帴鏍囪瘑鍚?/summary>
	[ExpressionField]
	public string ConnectionName { get; set; } = "Modbus1";

	/// <summary>鎵归噺鍐欏叆椤瑰垪琛?/summary>
	public List<ModbusBatchWriteItem> Items { get; set; } = new();

	/// <summary>鍐欏叆闂撮殧(ms)锛?=鏃犻棿闅?/summary>
	public int IntervalMs { get; set; } = 0;
}
