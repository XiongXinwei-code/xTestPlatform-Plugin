using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsRoutineControlPlugin : StepPluginBase<UdsRoutineControlSetting>
{
    public override string StepTypeId => "UDS.RoutineControl";
    public override string DisplayName => "UDS_RoutineControl";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        执行 ECU 例程控制（UDS 服务 0x31）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ControlType | 枚举 | 否 | Start | 可选值：Start, Stop, RequestResults |
        | RoutineId | 表达式(string) | 是 | — | 例程 ID，如 0xFF00 |
        | OptionRecord | 表达式(string) | 否 | 空 | 输入参数（十六进制），可为空 |
        | ResultVariable | string | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据） |
        | ConnectionName | 表达式(string) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | 表达式(string) | 是 | — | 请求 CAN ID |
        | RxId | 表达式(string) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 响应超时毫秒数 |

        ## 行为

        - ECU 返回负响应或超时时步骤报错

        ## 相关插件

        - `UDS_DiagSession`：切换诊断会话
        - `UDS_SecurityAccess`：解锁受保护例程
        """;

    public override IStepExecutor CreateExecutor() => new UdsRoutineControlExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RoutineControl {s.ControlType} RID={s.RoutineId} → {s.ResultVariable}";
    }
}
