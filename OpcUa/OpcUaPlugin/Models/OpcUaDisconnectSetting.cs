using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace OpcUa.Models;

/// <summary>OPC UA 断开连接步骤的设置参数</summary>
[MessagePackObject(true)]
public class OpcUaDisconnectSetting
{
    /// <summary>要断开的连接名称</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"OpcUa1\"";
}
