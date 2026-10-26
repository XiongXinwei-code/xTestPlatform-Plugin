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
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa_open.png";

    public override string Description => """
        ## 功能

        打开 VISA 仪器会话，支持 GPIB、USB-TMC、TCP/LAN(SOCKET/INSTR)、串口等资源，打开后通过 ConnectionName 标识此连接。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | VISA1 | 连接标识名，序列内唯一，求值结果为 string |
        | ResourceString | string([ExpressionField]) | 是 | — | VISA 资源字符串，如 TCPIP::192.168.1.1::INSTR、GPIB0::1::INSTR，求值结果为 string |
        | OpenTimeoutMs | int | 否 | 5000 | 包含登记、旧收尾和打开的总期限；-1 为无限打开，此时 IoTimeoutMs 必须有限 |
        | IoTimeoutMs | int | 否 | 10000 | I/O 超时毫秒数，-1 为无限等待；不能与 OpenTimeoutMs 同时为 -1 |
        | Terminator | string | 否 | \n | 终止符 |

        ## 行为

        - 资源不存在或打开超时时步骤报错
        - 打开的会话会以 `ConnectionName` 为标识名注册到运行期资源表（同时以同一标识名注册 `Terminator` 配置），供后续 VISA_Write / VISA_Read / VISA_Query / VISA_Close 步骤取用
        - 同名重开先在 OpenTimeoutMs 同一预算内确认捕获的旧会话真实关闭；关闭失败或调用未退出时保留原 owner 并报错，不静默丢弃句柄

        ### 检索关键词

        VISA、NI-VISA、Keysight IO Libraries、Resource String、资源字符串、
        GPIB、IEEE-488、USB-TMC、USBTMC、TCPIP、INSTR、SOCKET、ASRL、
        SCPI、仪器控制、可编程仪器、万用表、程控电源、示波器、电子负载

        ## 相关插件

        - `VISA_Write` / `VISA_Read` / `VISA_Query` / `VISA_BatchWrite` / `VISA_WaitOPC`：在此连接上操作
        - `VISA_Close`：关闭本插件打开的会话
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("VISA", () => new VisaOpenExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.ResourceString})";
    }
}
