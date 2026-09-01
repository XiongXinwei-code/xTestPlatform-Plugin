using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanOpenSetting
{
    /// <summary>硬件适配器类型</summary>
    public CanAdapterType AdapterType { get; set; } = CanAdapterType.NI;

    /// <summary>CAN 通道名称（如 CAN1、PCAN_USBBUS1）</summary>
    [ExpressionField]
    public string Channel { get; set; } = "\"CAN1\"";

    /// <summary>仲裁段波特率</summary>
    public int BaudRate { get; set; } = 500_000;

    /// <summary>协议类型</summary>
    public CanProtocolType Protocol { get; set; } = CanProtocolType.Classic;

    /// <summary>数据段波特率（CAN FD 有效）</summary>
    public int DataBitRate { get; set; } = 2_000_000;

    /// <summary>使能硬件内置 120 Ω 终端电阻（当前适用于支持该功能的 NI-XNET 设备）</summary>
    public bool EnableTermination { get; set; }

    /// <summary>仲裁段位时序配置方式</summary>
    public CanBitTimingMode ArbitrationBitTimingMode { get; set; } = CanBitTimingMode.Automatic;

    /// <summary>快捷模式下的仲裁段目标采样点（百分比）</summary>
    public double ArbitrationSamplePoint { get; set; } = 80.0;

    /// <summary>手动模式下的仲裁段 BRP 寄存器值</summary>
    public int ArbitrationBrp { get; set; } = 1;

    /// <summary>手动模式下的仲裁段 SJW 寄存器值</summary>
    public int ArbitrationSjw { get; set; } = 4;

    /// <summary>手动模式下的仲裁段 TSEG1 寄存器值</summary>
    public int ArbitrationTseg1 { get; set; } = 30;

    /// <summary>手动模式下的仲裁段 TSEG2 寄存器值</summary>
    public int ArbitrationTseg2 { get; set; } = 7;

    /// <summary>接收缓冲区大小（帧数，驱动层接收队列容量）</summary>
    public int RxQueueSize { get; set; } = 512;

    /// <summary>运行时连接标识名（供其他步骤引用）</summary>
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"CAN1\"";
}
