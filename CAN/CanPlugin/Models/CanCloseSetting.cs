using MessagePack;

namespace CAN.Models;

[MessagePackObject(true)]
public class CanCloseSetting
{
    /// <summary>要关闭的连接标识名</summary>
    public string ConnectionName { get; set; } = "CAN1";
}
