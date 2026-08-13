using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsSecurityAccessPlugin : StepPluginBase<UdsSecurityAccessSetting>
{
    public override string StepTypeId => "UDS.SecurityAccess";
    public override string DisplayName => "UDS_SecurityAccess";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU，自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SecurityLevel | int | 否 | 1 | 安全等级，奇数如 1/3/5 |
        | SeedVariable | string | 是 | — | 存储 ECU 返回 Seed 的变量名，KeyExpression 中通过此名引用 |
        | KeyExpression | 表达式(byte[]) | 是 | — | Key 计算表达式，如 new byte[]{(byte)(Seed[0]^0xA5)} |
        | ResultVariable | string | 否 | 空 | 结果变量名，写入类型为 bool（解锁是否成功） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - 先请求 Seed，用 KeyExpression 计算 Key 后发送，ECU 确认后解锁成功
        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_DiagSession`：先切换到非默认会话
        - `UDS_WriteDataByID` / `UDS_RoutineControl`：解锁后执行受保护操作
        """;

    public override IStepExecutor CreateExecutor() => new UdsSecurityAccessExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SecurityAccess Level={s.SecurityLevel} (TX={s.TxId}, RX={s.RxId})";
    }
}
