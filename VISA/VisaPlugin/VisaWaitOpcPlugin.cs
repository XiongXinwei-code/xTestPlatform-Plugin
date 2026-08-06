using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 等待操作完成插件，发送 *OPC? 并等待仪器返回 1
/// </summary>
public sealed class VisaWaitOpcPlugin : StepPluginBase<VisaWaitOpcSetting>
{
    public override string StepTypeId => "IO.VisaWaitOpc";
    public override string DisplayName => "VISA_WaitOPC";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        等待仪器当前操作完成（发送 *OPC? 并等待返回 '1'），用于校准、测量等耗时操作的同步。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | — | 已打开的 VISA 连接标识名 |
        | TimeoutMs | int | 否 | 0 | 等待超时毫秒数，0 表示使用 Open 时设置的默认 IO 超时 |

        ## 行为

        - 仪器返回 '1' 表示所有挂起的操作已完成，步骤通过
        - 等待超时或连接不存在时步骤报错

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Write`：发送耗时操作命令后配合本插件同步
        """;

    public override IStepExecutor CreateExecutor() => new VisaWaitOpcExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WaitOPC {s.ConnectionName}";
    }
}
