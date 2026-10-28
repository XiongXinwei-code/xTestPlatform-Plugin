using MessagePack;
using System.Collections.ObjectModel;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinCyclicSendStartSetting
{
    /// <summary>登记门与旧周期任务退出共用总预算，默认3000ms；未确认退出不得启动替代任务</summary>
    public int ReplaceTimeoutMs { get; set; } = 3000;
    /// <summary>连接标识名</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";

    /// <summary>任务标识名（Stop 时用此名称停止）</summary>
    [ExpressionField]
    public string TaskName { get; set; } = "\"LinCyclicTask1\"";

    /// <summary>周期发送帧列表</summary>
    public ObservableCollection<LinCyclicFrameItem> Frames { get; set; } = [];
}
