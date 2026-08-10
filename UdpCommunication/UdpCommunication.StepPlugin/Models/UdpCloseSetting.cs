using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace UdpCommunication.Models;

[MessagePackObject(true)]
public class UdpCloseSetting
{
    /// <summary>引用的 UDP_Open 步骤地址（运行时根据此地址在 RuntimeData 中查找 Transport）。</summary>
    public string OpenStepAddress { get; set; } = string.Empty;
}
