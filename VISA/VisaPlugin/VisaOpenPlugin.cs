using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 打开会话插件，通过 Resource String 建立与仪器的连接
/// </summary>
public sealed class VisaOpenPlugin : StepPluginBase<VisaOpenSetting>
{
    public override string StepTypeId => "IO.VisaOpen";
    public override string DisplayName => "VISA_Open";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        打开 VISA 仪器会话，支持 GPIB、USB-TMC、TCP/LAN(SOCKET/INSTR)、串口等资源，打开后通过 ConnectionName 标识此连接。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | VISA1 | 连接标识名，序列内唯一 |
        | ResourceString | string([ExpressionField]) | 是 | — | VISA 资源字符串，如 TCPIP::192.168.1.1::INSTR、GPIB0::1::INSTR |
        | OpenTimeoutMs | int | 否 | 5000 | 打开超时毫秒数 |
        | IoTimeoutMs | int | 否 | 3000 | IO 超时毫秒数 |
        | Terminator | string | 否 | \n | 终止符 |

        ## 行为

        - 资源不存在或打开超时时步骤报错
        - 同名 ConnectionName 重复打开会报错，需先用 VISA_Close 关闭

        ## 相关插件

        - `VISA_Write` / `VISA_Read` / `VISA_Query` / `VISA_BatchWrite` / `VISA_WaitOPC`：在此连接上操作
        - `VISA_Close`：关闭本插件打开的会话
        """;

    public override IStepExecutor CreateExecutor() => new VisaOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.ResourceString})";
    }
}
