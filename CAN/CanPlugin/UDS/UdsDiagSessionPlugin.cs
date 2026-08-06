using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsDiagSessionPlugin : StepPluginBase<UdsDiagSessionSetting>
{
    public override string StepTypeId => "UDS.DiagSession";
    public override string DisplayName => "UDS_DiagSession";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        切换 ECU 诊断会话模式（UDS 服务 0x10）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionType | 枚举 | 否 | Extended | 可选值：Default, Programming, Extended |
        | SuppressPositiveResponse | bool | 否 | false | 是否抑制正响应 |
        | ConnectionName | 表达式(string) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | 表达式(string) | 是 | — | 请求 CAN ID，如 0x7DF |
        | RxId | 表达式(string) | 是 | — | 响应 CAN ID，如 0x7E8 |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `UDS_SecurityAccess`：会话切换后执行安全访问
        """;

    public override IStepExecutor CreateExecutor() => new UdsDiagSessionExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DiagSession → {s.SessionType} (TX={s.TxId}, RX={s.RxId})";
    }
}
