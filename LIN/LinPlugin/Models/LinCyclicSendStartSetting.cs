using MessagePack;
using System.Collections.ObjectModel;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCyclicSendStartSetting
{
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>任务标识名（Stop 时用此名称停止）</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"LinCyclicTask1\"";

    /// <summary>是否将发送信息输出到 Log 窗口</summary>
    public bool EnableLog { get; set; } = false;

    /// <summary>周期发送帧列表</summary>
    public ObservableCollection<LinCyclicFrameItem> Frames { get; set; } = [];
}
