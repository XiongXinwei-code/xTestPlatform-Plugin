using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace LIN.Models;

[MessagePackObject(true)]
public class LinOpenSetting
{
    /// <summary>硬件适配器类型</summary>
    public LinAdapterType AdapterType { get; set; } = LinAdapterType.NI;

    /// <summary>LIN 通道名称（如 LIN1、PLIN_USBBUS1）</summary>
    [ExpressionField]
    public string Channel { get; set; } = "\"LIN1\"";

    /// <summary>波特率（常用：2400、9600、10400、19200）</summary>
    public int BaudRate { get; set; } = 19200;

    /// <summary>LIN 协议版本</summary>
    public LinVersionType LinVersion { get; set; } = LinVersionType.LIN_2x;

    /// <summary>是否作为主节点运行（否则为从节点）</summary>
    public bool IsMaster { get; set; } = true;

    /// <summary>接收缓冲区大小（帧数，驱动层接收队列容量）</summary>
    public int RxQueueSize { get; set; } = 512;

    /// <summary>运行时连接标识名（供其他步骤引用）</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"LIN1\"";
}
