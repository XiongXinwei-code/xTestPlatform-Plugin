using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDiagRequestPlugin : StepPluginBase<DoipDiagRequestSetting>
{
    public override string StepTypeId  => "DoIP.DiagRequest";
    public override string DisplayName => "DoIP_DiagRequest";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description => """
        ## 功能

        通过已建立的 DoIP 会话发送 UDS 诊断请求并接收响应。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | string([ExpressionField]) | 是 | "DOIP1" | 已建立的会话标识名 |
        | TargetAddress | string([ExpressionField]) | 是 | "0x1000" | ECU 逻辑地址 |
        | RequestData | string([ExpressionField]) | 是 | "22 F1 90" | UDS 请求十六进制数据 |
        | TimeoutMs | int | 否 | 3000 | 响应超时毫秒数 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据） |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 收到 UDS 负响应（0x7F）时步骤判定为 Failed
        - 超时未收到响应时步骤报错

        ## 相关插件

        - `DoIP_Connect`：建立 DoIP 会话
        - `DoIP_Disconnect`：关闭 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => new DoipDiagRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP DiagRequest: {s.SessionName} -> {s.TargetAddress} [{s.RequestData}]";
    }
}
