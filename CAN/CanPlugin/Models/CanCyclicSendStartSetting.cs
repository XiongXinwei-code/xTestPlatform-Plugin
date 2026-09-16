using MessagePack;
using System.Collections.ObjectModel;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanCyclicSendStartSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";

    /// <summary>任务标识名（Stop 时用此名称停止）</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"CyclicTask1\"";

    /// <summary>周期发送报文列表</summary>
    public ObservableCollection<CyclicMessageItem> Messages { get; set; } = [];
}
