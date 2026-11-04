using MessagePack;
using System.Collections.ObjectModel;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public class CanCyclicSendStartSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>任务标识名（Stop 时用此名称停止）</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"CyclicTask1\"";

    /// <summary>动态启用/周期的检查间隔，不是硬实时保证。</summary>
    public int ControlRefreshMs { get; set; } = 20;

    /// <summary>单次动态表达式求值期限。</summary>
    public int ExpressionTimeoutMs { get; set; } = 5000;

    /// <summary>周期发送报文列表</summary>
    public ObservableCollection<CyclicMessageItem> Messages { get; set; } = [];
}
