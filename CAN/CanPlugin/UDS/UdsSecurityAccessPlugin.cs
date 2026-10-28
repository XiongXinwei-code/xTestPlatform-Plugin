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
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/uds_security_access.png";

    public override string Description => """
        ## 功能

        执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU，自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SecurityLevel | int | 否 | 1 | 等级序号 1~63；Seed 子功能=2×等级−1，Key=2×等级；不是原始奇数子功能值，保留既有映射 |
        | SeedVariable | string(VariablePathField) | 是 | — | 存储 ECU 返回 Seed 的变量名，KeyExpression 中通过此名引用；如 Locals.seed |
        | KeyExpression | string([ExpressionField]) | 是 | — | Key 计算表达式，如 new byte[]{(byte)(Seed[0]^0xA5)}，求值结果为 byte[] |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名，写入类型为 bool（解锁是否成功）；如 Locals.result |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名，求值结果为 string |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID，如 0x7DF，求值结果为 string |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID，如 0x7E8，求值结果为 string |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |
        | TransactionTimeoutMs | int | 否 | 60000 | 单个 UDS 事务总期限，包含排队、发送、响应及 NRC 0x78；必须大于 0，不是整个 Flash 步骤期限 |
        | MaxResponseBytes | int | 否 | 4194304 | ISO-TP 响应重组上限（字节），必须大于 0；编辑器连接区可配置，超限在分配前拒绝；不是下载块大小 |

        ## 行为

        - 先请求 Seed，用 KeyExpression 计算 Key 后发送，ECU 确认后解锁成功
        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_DiagSession`：先切换到非默认会话
        - `UDS_WriteDataByID` / `UDS_RoutineControl`：解锁后执行受保护操作
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new UdsSecurityAccessExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SecurityAccess Level={s.SecurityLevel} (TX={s.TxId}, RX={s.RxId})";
    }
}
