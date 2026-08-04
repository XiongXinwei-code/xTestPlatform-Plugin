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

    public override string Description =>
        "执行 ECU 例程控制（UDS 服务 0x31）。" +
        "Setting 字段：ControlType(枚举:Start/Stop/RequestResults,默认Start), RoutineId(string,表达式,例程ID如0xFF00), " +
        "OptionRecord(string,表达式,输入参数十六进制,可为空), ResultVariable(string,结果变量名,写入类型:string 十六进制响应数据,可选), " +
        "ConnectionName(string,表达式,已打开的CAN连接名), TxId(string,表达式,请求CAN ID), RxId(string,表达式,响应CAN ID), ResponseTimeoutMs(int,响应超时,默认5000)。";

    public override IStepExecutor CreateExecutor() => new UdsRoutineControlExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RoutineControl {s.ControlType} RID={s.RoutineId} → {s.ResultVariable}";
    }
}
