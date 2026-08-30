using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsRawRequestPlugin : StepPluginBase<UdsRawRequestSetting>
{
    public override string StepTypeId => "UDS.RawRequest";
    public override string DisplayName => "UDS_RawRequest";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        发送原始 UDS 请求数据（通用，任意服务），适用于其他专用 UDS 插件未覆盖的服务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | RequestData | string([ExpressionField]) | 是 | — | 十六进制请求数据，如 "10 03" |
        | WaitResponse | bool | 否 | true | 是否等待响应 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - WaitResponse=true 时等待 ECU 响应，负响应或超时则步骤报错

        ## 相关插件

        - `UDS_DiagSession` / `UDS_ReadDataByID` / `UDS_WriteDataByID`：常用服务的专用插件
        """;

    public override IStepExecutor CreateExecutor() => new UdsRawRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RawRequest [{s.RequestData}] → {s.ResultVariable}";
    }
}
