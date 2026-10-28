using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Ethernet.DoIP.Models;

/// <summary>DoIP_Disconnect 步骤设置</summary>
[MessagePackObject(true)]
public class DoipDisconnectSetting
{
    /// <summary>要断开的会话标识名</summary>
    [ExpressionField]
    public string SessionName { get; set; } = "\"DOIP1\"";
    /// <summary>等待关闭实际完成的总期限（毫秒），必须大于0。</summary>
    public int TimeoutMs { get; set; } = 5000;
}
