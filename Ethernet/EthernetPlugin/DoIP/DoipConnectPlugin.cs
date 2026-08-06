using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipConnectPlugin : StepPluginBase<DoipConnectSetting>
{
    public override string StepTypeId  => "DoIP.Connect";
    public override string DisplayName => "DoIP_Connect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        建立 DoIP（ISO 13400）TCP 连接并执行路由激活，以 SessionName 注册会话供后续步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | 表达式(string) | 是 | "DOIP1" | 会话标识名 |
        | RemoteHost | 表达式(string) | 是 | "192.168.1.10" | DoIP 实体 IP |
        | RemotePort | 表达式(string) | 是 | "13400" | TCP 端口 |
        | SourceAddress | 表达式(string) | 是 | "0x0E00" | 诊断仪逻辑地址 |
        | ActivationType | 枚举 | 否 | Default | 可选值：Default, WwhObd, CentralSecurity |
        | TimeoutMs | int | 否 | 3000 | 超时毫秒数 |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 连接或路由激活失败时步骤报错

        ## 相关插件

        - `DoIP_DiagRequest`：发送 UDS 诊断请求
        - `DoIP_Disconnect`：关闭 DoIP 会话
        - `DoIP_VehicleDiscovery`：发现车辆 DoIP 实体
        """;

    public override IStepExecutor CreateExecutor() => new DoipConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Connect: {s.SessionName} -> {s.RemoteHost}:{s.RemotePort} SA={s.SourceAddress}";
    }
}
